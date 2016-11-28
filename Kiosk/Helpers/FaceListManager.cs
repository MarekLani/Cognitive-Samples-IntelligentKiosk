// 
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// 
// Microsoft Cognitive Services: http://www.microsoft.com/cognitive
// 
// Microsoft Cognitive Services Github:
// https://github.com/Microsoft/Cognitive
// 
// Copyright (c) Microsoft Corporation
// All rights reserved.
// 
// MIT License:
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// 

using IntelligentKioskSample;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ServiceHelpers
{
    internal class FaceListInfo
    {
        public string FaceListId { get; set; }
        public DateTime LastMatchTimestamp { get; set; }
        public bool IsFull { get; set; }
    }

    public class FaceListManager
    {

        private static string newFaceListId;

        private static Dictionary<string, FaceListInfo> faceLists;

        public static string FaceListsUserDataFilter { get; set; }

        private FaceListManager() { }

        public static async Task ResetFaceLists()
        {
            faceLists = new Dictionary<string, FaceListInfo>();

            try
            {
                //IEnumerable<FaceListMetadata> metadata = await FaceServiceHelper.GetFaceListsAsync(FaceListsUserDataFilter);
                IEnumerable<FaceListMetadata> metadata = await FaceServiceHelper.GetFaceListsAsync();
                foreach (var item in metadata)
                {
                    await FaceServiceHelper.DeleteFaceListAsync(item.FaceListId);
                }

                //We want to delete also local persisted DB
                using (var db = new KioskDBContext())
                {
                    db.SimilarFaces.RemoveRange(db.SimilarFaces);
                    db.SaveChanges();
                }
            }
            catch (Exception e)
            {
                ErrorTrackingHelper.TrackException(e, "Error resetting face lists");
            }
        }

        public static async Task Initialize()
        {
            faceLists = new Dictionary<string, FaceListInfo>();

            try
            {
                //IEnumerable<FaceListMetadata> metadata = await FaceServiceHelper.GetFaceListsAsync(FaceListsUserDataFilter);
                IEnumerable<FaceListMetadata> metadata = await FaceServiceHelper.GetFaceListsAsync();
                foreach (var item in metadata)
                {
                    faceLists.Add(item.FaceListId, new FaceListInfo { FaceListId = item.FaceListId, LastMatchTimestamp = DateTime.Now });
                }

            }
            catch (Exception e)
            {
                ErrorTrackingHelper.TrackException(e, "Face API GetFaceListsAsync error");
            }
        }

        public static async Task DeleteFaceFromFaceList(string faceIdToDelete)
        {
            if (faceLists == null)
            {
                await Initialize();
            }

            var faceListId = faceLists.FirstOrDefault().Key;
            await FaceServiceHelper.DeleteFaceFromFaceListAsync(faceListId, new Guid(faceIdToDelete));
        }

        public static async Task<Tuple<SimilarPersistedFace, string>> FindSimilarPersistedFaceAsync(Stream imageStream, Guid faceId, FaceRectangle faceRectangle)
        {
            if (faceLists == null)
            {
                await Initialize();
            }

            Tuple<SimilarPersistedFace, string> bestMatch = null;

            var faceListId = faceLists.FirstOrDefault().Key;

            //Delete for testing purposes
            //await FaceServiceHelper.DeleteFaceListAsync(faceListId);
            try
            {
                SimilarPersistedFace similarFace = null;
                if(faceListId != null)
                    similarFace = (await FaceServiceHelper.FindSimilarAsync(faceId, faceListId))?.FirstOrDefault();
                if (similarFace != null)
                {
                    if (bestMatch != null)
                    {
                        // We already found a match for this face in another list. Replace the previous one if the new confidence is higher.
                        if (bestMatch.Item1.Confidence < similarFace.Confidence)
                        {
                            bestMatch = new Tuple<SimilarPersistedFace, string>(similarFace, faceListId);
                        }
                    }
                    else
                    {
                        bestMatch = new Tuple<SimilarPersistedFace, string>(similarFace, faceListId);
                    }
                }
                else
                {
                    // If we are here we didnt' find a match, so let's add the face to the first FaceList that we can add it to. We
                    // might create a new list if none exist, and if all lists are full we will delete the oldest face list (based on when we  
                    // last matched anything on it) so that we can add the new one.

                    if (!faceLists.Any())
                    {
                        // We don't have any FaceLists yet. Create one
                        newFaceListId = Guid.NewGuid().ToString();
                        // await FaceServiceHelper.CreateFaceListAsync(newFaceListId, "ManagedFaceList", FaceListsUserDataFilter);
                        //We are not using filters
                        await FaceServiceHelper.CreateFaceListAsync(newFaceListId, "ManagedFaceList");

                        faceLists.Add(newFaceListId, new FaceListInfo { FaceListId = newFaceListId, LastMatchTimestamp = DateTime.Now });
                    }

                    AddPersistedFaceResult addResult = null;

                    var faceList = faceLists.First();
                    if (faceList.Value.IsFull)
                    {
                        DBSimilarFace faceToDelete = null;
                        using (var db = new KioskDBContext())
                        {
                            faceToDelete = db.SimilarFaces.OrderByDescending(sf => sf.PersonId).First();
                            db.SimilarFaces.Remove(faceToDelete);
                            await db.SaveChangesAsync();
                        }

                        await FaceServiceHelper.DeleteFaceFromFaceListAsync(faceList.Key, new Guid(faceToDelete.FaceId));
                    }

                    addResult = await FaceServiceHelper.AddFaceToFaceListAsync(faceList.Key, imageStream, faceRectangle);

                    if (addResult != null)
                    {
                        bestMatch = new Tuple<SimilarPersistedFace, string>(new SimilarPersistedFace { Confidence = 1, PersistedFaceId = addResult.PersistedFaceId }, null);
                    }
                }
            }
            catch (Exception e)
            {
                // Catch errors with individual face lists so we can continue looping through all lists. Maybe an answer will come from
                // another one.
                ErrorTrackingHelper.TrackException(e, "Face API FindSimilarAsync error");
            }

            return bestMatch;
        }
    }
}
