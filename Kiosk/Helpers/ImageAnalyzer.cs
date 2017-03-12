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
using Microsoft.ProjectOxford.Common;
using Microsoft.ProjectOxford.Emotion.Contract;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace ServiceHelpers
{
    public class ImageAnalyzer
    {
        public static FaceAttributeType[] DefaultFaceAttributeTypes = new FaceAttributeType[] { FaceAttributeType.Age, FaceAttributeType.FacialHair, FaceAttributeType.Glasses, FaceAttributeType.Smile, FaceAttributeType.Gender, FaceAttributeType.HeadPose };
        private string groupId = null;

        public event EventHandler FaceDetectionCompleted;
        public event EventHandler FaceRecognitionCompleted;
        public event EventHandler EmotionRecognitionCompleted;

        public static string PeopleGroupsUserDataFilter = null;

        public Func<Task<Stream>> GetImageStreamCallback { get; set; }
        public string LocalImagePath { get; set; }
        public string ImageUrl { get; set; }

        public IEnumerable<Face> DetectedFaces { get; set; }

        public IEnumerable<Emotion> DetectedEmotion { get; set; }

        public IEnumerable<IdentifiedPerson> IdentifiedPersons { get; set; }

        //public IdentifyResult Candidates { get; set; }

        public IEnumerable<SimilarFaceMatch> SimilarFaceMatches { get; set; }

        // Default to no errors, since this could trigger a stream of popup errors since we might call this
        // for several images at once while auto-detecting the Bing Image Search results.
        public bool ShowDialogOnFaceApiErrors { get; set; } = false;

        public bool FilterOutSmallFaces { get; set; } = false;

        public int DecodedImageHeight { get; private set; }
        public int DecodedImageWidth { get; private set; }
        public byte[] Data { get; set; }

        public ImageAnalyzer(string url)
        {
            this.ImageUrl = url;
        }

        public ImageAnalyzer(Func<Task<Stream>> getStreamCallback, string path = null)
        {
            this.GetImageStreamCallback = getStreamCallback;
            this.LocalImagePath = path;
        }

        public ImageAnalyzer(byte[] data)
        {
            this.Data = data;
            this.GetImageStreamCallback = () => Task.FromResult<Stream>(new MemoryStream(this.Data));
        }

        public void UpdateDecodedImageSize(int height, int width)
        {
            this.DecodedImageHeight = height;
            this.DecodedImageWidth = width;
        }

        public async Task DetectFacesAsync(bool detectFaceAttributes = false)
        {
            try
            {
                if (this.ImageUrl != null)
                {
                    this.DetectedFaces = await FaceServiceHelper.DetectAsync(
                        this.ImageUrl,
                        returnFaceId: true,
                        returnFaceLandmarks: false,
                        returnFaceAttributes: DefaultFaceAttributeTypes);
                }
                else if (this.GetImageStreamCallback != null)
                {
                    this.DetectedFaces = await FaceServiceHelper.DetectAsync(
                        await this.GetImageStreamCallback(),
                        returnFaceId: true,
                        returnFaceLandmarks: false,
                        //returnFaceAttributes: detectFaceAttributes ? DefaultFaceAttributeTypes : null);
                        returnFaceAttributes: DefaultFaceAttributeTypes);
                }

                if (this.FilterOutSmallFaces)
                {
                    this.DetectedFaces = this.DetectedFaces.Where(f => CoreUtil.IsFaceBigEnoughForDetection(f.FaceRectangle.Height, this.DecodedImageHeight));
                }
            }
            catch (Exception e)
            {
                ErrorTrackingHelper.TrackException(e, "Face API DetectAsync error");

                this.DetectedFaces = Enumerable.Empty<Face>();

                if (this.ShowDialogOnFaceApiErrors)
                {
                    await ErrorTrackingHelper.GenericApiCallExceptionHandler(e, "Face detection failed.");
                }
            }
            finally
            {
                this.OnFaceDetectionCompleted();
            }
        }

        public async Task DetectEmotionAsync()
        {
            try
            {
                if (this.ImageUrl != null)
                {
                    this.DetectedEmotion = await EmotionServiceHelper.RecognizeAsync(this.ImageUrl);
                }
                else if (this.GetImageStreamCallback != null)
                {
                    this.DetectedEmotion = await EmotionServiceHelper.RecognizeAsync(await this.GetImageStreamCallback());
                }

                if (this.FilterOutSmallFaces)
                {
                    this.DetectedEmotion = this.DetectedEmotion.Where(f => CoreUtil.IsFaceBigEnoughForDetection(f.FaceRectangle.Height, this.DecodedImageHeight));
                }
            }
            catch (Exception e)
            {
                ErrorTrackingHelper.TrackException(e, "Emotion API RecognizeAsync error");

                this.DetectedEmotion = Enumerable.Empty<Emotion>();

                if (this.ShowDialogOnFaceApiErrors)
                {
                    await ErrorTrackingHelper.GenericApiCallExceptionHandler(e, "Emotion detection failed.");
                }
            }
            finally
            {
                this.OnEmotionRecognitionCompleted();
            }
        }

        /// <summary>
        /// Calling emotion detect for every recognized face
        /// </summary>
        /// <returns></returns>
        public async Task DetectEmotionWithRectanglesAsync()
        {
            try
            {
                var rectangles = new List<Rectangle>();
                foreach (var f in this.DetectedFaces)
                {
                    Rectangle r = new Rectangle() { Top = f.FaceRectangle.Top, Height = f.FaceRectangle.Height, Left = f.FaceRectangle.Left, Width = f.FaceRectangle.Width };
                    rectangles.Add(r);
                }
                if (this.ImageUrl != null)
                {
                    this.DetectedEmotion = await EmotionServiceHelper.RecognizeWithFaceRectanglesAsync(this.ImageUrl,rectangles.ToArray());
                }
                else if (this.GetImageStreamCallback != null)
                {
                    this.DetectedEmotion = await EmotionServiceHelper.RecognizeWithFaceRectanglesAsync(await this.GetImageStreamCallback(),rectangles.ToArray());
                }

                if (this.FilterOutSmallFaces)
                {
                    this.DetectedEmotion = this.DetectedEmotion.Where(f => CoreUtil.IsFaceBigEnoughForDetection(f.FaceRectangle.Height, this.DecodedImageHeight));
                }
            }
            catch (Exception e)
            {
                ErrorTrackingHelper.TrackException(e, "Emotion API RecognizeAsync error");

                this.DetectedEmotion = Enumerable.Empty<Emotion>();

                if (this.ShowDialogOnFaceApiErrors)
                {
                    await ErrorTrackingHelper.GenericApiCallExceptionHandler(e, "Emotion detection failed.");
                }
            }
            finally
            {
                this.OnEmotionRecognitionCompleted();
            }
        }

        public async Task IdentifyFacesAsync()
        {
            this.IdentifiedPersons = Enumerable.Empty<IdentifiedPerson>();

            Guid[] detectedFaceIds = this.DetectedFaces?.Select(f => f.FaceId).ToArray();
            if (detectedFaceIds != null && detectedFaceIds.Any())
            {
                List<IdentifiedPerson> result = new List<IdentifiedPerson>();

                IEnumerable<PersonGroup> personGroups = Enumerable.Empty<PersonGroup>();
                try
                {
                    personGroups = await FaceServiceHelper.GetPersonGroupsAsync(PeopleGroupsUserDataFilter);
                }
                catch (Exception e)
                {
                    ErrorTrackingHelper.TrackException(e, "Face API GetPersonGroupsAsync error");

                    if (this.ShowDialogOnFaceApiErrors)
                    {
                        await ErrorTrackingHelper.GenericApiCallExceptionHandler(e, "Failure getting PersonGroups");
                    }
                }

                foreach (var group in personGroups)
                {
                    try
                    {
                        IdentifyResult[] groupResults = await FaceServiceHelper.IdentifyAsync(group.PersonGroupId, detectedFaceIds);
                        foreach (var match in groupResults)
                        {
                            if (!match.Candidates.Any())
                            {
                                continue;
                            }

                            Person person = await FaceServiceHelper.GetPersonAsync(group.PersonGroupId, match.Candidates[0].PersonId);

                            IdentifiedPerson alreadyIdentifiedPerson = result.FirstOrDefault(p => p.Person.PersonId == match.Candidates[0].PersonId);
                            if (alreadyIdentifiedPerson != null)
                            {
                                // We already tagged this person in another group. Replace the existing one if this new one if the confidence is higher.
                                if (alreadyIdentifiedPerson.Confidence < match.Candidates[0].Confidence)
                                {
                                    alreadyIdentifiedPerson.Person = person;
                                    alreadyIdentifiedPerson.Confidence = match.Candidates[0].Confidence;
                                    alreadyIdentifiedPerson.FaceId = match.FaceId;
                                }
                            }
                            else
                            {
                                result.Add(new IdentifiedPerson { Person = person, Confidence = match.Candidates[0].Confidence, FaceId = match.FaceId });
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        // Catch errors with individual groups so we can continue looping through all groups. Maybe an answer will come from
                        // another one.
                        ErrorTrackingHelper.TrackException(e, "Face API IdentifyAsync error");

                        if (this.ShowDialogOnFaceApiErrors)
                        {
                            await ErrorTrackingHelper.GenericApiCallExceptionHandler(e, "Failure identifying faces");
                        }
                    }
                }

                this.IdentifiedPersons = result;
            }

            this.OnFaceRecognitionCompleted();
        }

        public async Task<List<FaceSendInfo>> FindSimilarPersonWithEmotion()
        {
            var facesInfo = new List<FaceSendInfo>();
            List<SimilarFaceMatch> result = new List<SimilarFaceMatch>();
            //Loop thru all detected faces from previous steps and fill the facesInfo array
            //For ease of processing in Azure Stream Analytics we create single level object
            foreach (var f in this.DetectedFaces)
            {
                var fsi = new FaceSendInfo();

                //Add emotions
                var e = CoreUtil.FindEmotionForFace(f, this.DetectedEmotion);
                FeedFaceInfo(f, fsi, e);
                //We sen also info how many faces in total were recognized on the picture with current face
                fsi.facesNo = this.DetectedFaces.Count();

               

                Tuple<SimilarPersistedFace, string> similarPersistedFace = await FaceListManager.FindSimilarPersistedFaceAsync(await this.GetImageStreamCallback(), f.FaceId, f.FaceRectangle);
                if (similarPersistedFace != null)
                {
                    result.Add(new SimilarFaceMatch { Face = f, SimilarPersistedFace = similarPersistedFace.Item1 });
                    fsi.canid = similarPersistedFace.Item1.PersistedFaceId.ToString();
                    fsi.canconf = similarPersistedFace.Item1.Confidence;

                    //In order to get also name we need to obtain Person
                    //p = await FaceServiceHelper.GetPersonAsync(groupId, can.PersonId);
                    fsi.canname = similarPersistedFace.Item1.PersistedFaceId.ToString();
                }

                facesInfo.Add(fsi);
            }
            SimilarFaceMatches = result;
            return facesInfo;
        }

        public async Task<List<FaceSendInfo>> IdentifyOrAddPersonWithEmotionsAsync(string groupName, ObservableCollection<IdentifiedFaces> identifiedPersonsIdCollection)
        {
                    
            var facesInfo = new List<FaceSendInfo>();

            //Loop thru all detected faces from previous steps and fill the facesInfo array
            //For ease of processing in Azure Stream Analytics we create single level object
            foreach (var f in this.DetectedFaces)
            {
                var fsi = new FaceSendInfo();

                //Add emotions
                var e = CoreUtil.FindEmotionForFace(f, this.DetectedEmotion);
                FeedFaceInfo( f, fsi, e);
                //We sen also info how many faces in total were recognized on the picture with current face
                fsi.facesNo = this.DetectedFaces.Count();

                facesInfo.Add(fsi);
            }

            //Now we proceed to face recognition/identification
            //First we create group if it does not exist
            try
            {
                var g = await FaceServiceHelper.CreatePersonGroupIfNoGroupExists(groupName);
                groupId = g.PersonGroupId;
            }
            catch (Exception e)
            {
                // Catch errors with individual groups so we can continue looping through all groups. Maybe an answer will come from
                // another one.
                ErrorTrackingHelper.TrackException(e, "Problem creating group");

                if (this.ShowDialogOnFaceApiErrors)
                {
                    await ErrorTrackingHelper.GenericApiCallExceptionHandler(e, "Problem creating group");
                }
            }

           
            //We need to find candidate for every face
            try
            {
                IdentifyResult[] groupResults = await this.IdentifyFacesAsync(groupId);

                //We loop thri all faces again in order to find candidate
                foreach (var f in this.DetectedFaces)
                {
                    bool needToRetrain = true;
                    var fi = facesInfo.Where(fin => fin.faceId == f.FaceId.ToString()).FirstOrDefault();
                    var newPersonID = Guid.NewGuid();

                    if (groupResults != null && groupResults.Where(gr => gr.FaceId == f.FaceId).Any() && groupResults.Where(gr => gr.FaceId == f.FaceId).FirstOrDefault().Candidates.Any())
                    {
                        var candidates = groupResults.Where(gr => gr.FaceId == f.FaceId).FirstOrDefault().Candidates.OrderByDescending(ca => ca.Confidence);
 
                        Person p = new Person();
                        var can = candidates.FirstOrDefault();

                        //If we have sufficient confidence, we add Face for person
                        if (can.Confidence >= SettingsHelper.Instance.Confidence)
                        {
                            fi.canid = can.PersonId.ToString();
                            fi.canconf = can.Confidence;

                            //In order to get also name we need to obtain Person
                            p = await FaceServiceHelper.GetPersonAsync(groupId, can.PersonId);
                            fi.canname = p.Name;

                            var identifiedPersonFromList = identifiedPersonsIdCollection.Where(ip => ip.Id == can.PersonId.ToString()).FirstOrDefault();

                            //Check whether we did not added too much photos lately, it is not neccesary to add photo for face every time
                            if (identifiedPersonFromList == null)
                            {
                                await AddFaceToPerson(f, p, can.PersonId);
                            }
                            else if(identifiedPersonFromList.NumOfAddedPhotosInLastPeriod < SettingsHelper.Instance.NumberOfPhotoAddsInPeriod)
                            {
                                await AddFaceToPerson(f, p, can.PersonId);
                                identifiedPersonFromList.NumOfAddedPhotosInLastPeriod++;
                            }
                            else if ((DateTime.Now - identifiedPersonFromList.FirstPhotoAddedInLastPeriod).Hours > SettingsHelper.Instance.PhotoAddPeriodSize)
                            {
                                identifiedPersonFromList.NumOfAddedPhotosInLastPeriod = 1;
                                identifiedPersonFromList.FirstPhotoAddedInLastPeriod = DateTime.Now;
                                await AddFaceToPerson(f, p, can.PersonId);
                            }
                            else
                            {
                                needToRetrain = false;
                            }
                        }
                        else
                        {
                            //if not sufficient confidence we also need to check whether there is similar face/ if not create new person
                            await CreatePrsonIfNoSimilarFaceExistsAsync(facesInfo, newPersonID, f);
                        }
                    }
                    else
                    {
                        //if no candidate we also need to check whether there is similar fac,e if not create new person
                        await CreatePrsonIfNoSimilarFaceExistsAsync(facesInfo, newPersonID, f);
                        
                    }
                    try
                    {
                        //We need to train after operation on top of group (addition of photo, person etc.)
                        if(needToRetrain)
                            await FaceServiceHelper.TrainPersonGroupAsync(groupId);
                    }
                    catch (Exception e)
                    {
                        // Catch error with training of group
                        ErrorTrackingHelper.TrackException(e, "Problem training group");

                        if (this.ShowDialogOnFaceApiErrors)
                        {
                            await ErrorTrackingHelper.GenericApiCallExceptionHandler(e, "Problem training group");
                        }
                    }


                    //Handle the identified persons collection to which we locally save every identified person 
                    if (!identifiedPersonsIdCollection.Where(ip => ip.Id == fi.canid).Any())
                    {
                        identifiedPersonsIdCollection.Add(new IdentifiedFaces() { Id = fi.canid });
                    }

                    //Increase counter of identifications
                    else if (identifiedPersonsIdCollection.Where(ip => ip.Id == fi.canid).Any())
                    {
                        identifiedPersonsIdCollection.Where(ip => ip.Id == fi.canid).FirstOrDefault().NumberOfIdentifications++;
                    }

                    //Find faces which were wrongly learned (small number of identifications)
                    var tbd = new List<IdentifiedFaces>();
                    foreach (var ip in identifiedPersonsIdCollection)
                    {
                        if (ip.NumberOfIdentifications <= SettingsHelper.Instance.NeededFaceIdentNum && (ip.CreatedAt.AddSeconds(SettingsHelper.Instance.DeleteWindow) < DateTime.Now))
                        {
                            var g = (await FaceServiceHelper.GetPersonGroupsAsync()).Where(gr => gr.Name == groupName).FirstOrDefault();
                            Person pers = await FaceServiceHelper.GetPersonAsync(g.PersonGroupId, new Guid(ip.Id));

                            //if we saved insufficient number of faces than delete
                            if (pers.PersistedFaceIds.Length <= SettingsHelper.Instance.NeededFaceIdentNum)
                            {
                                await FaceServiceHelper.DeletePersonAsync(g.PersonGroupId, pers.PersonId);

                                string similarFaceId = "";
                                using (var db = new KioskDBContext())
                                {
                                    var sfToDelete = db.SimilarFaces.Where(sf => sf.PersonId == pers.PersonId.ToString()).FirstOrDefault();
                                    similarFaceId = sfToDelete.FaceId.ToString();
                                    db.SimilarFaces.Remove(sfToDelete);
                                }

                                await FaceListManager.DeleteFaceFromFaceList(similarFaceId);
                                await FaceServiceHelper.TrainPersonGroupAsync(g.PersonGroupId);
                                tbd.Add(ip);
                            }
                        }
                    }


                    foreach (var iptodelete in tbd)
                    {
                        identifiedPersonsIdCollection.Remove(iptodelete);
                    }
                }
            }
            catch (Exception e)
            {
                // Catch error with training of group
                ErrorTrackingHelper.TrackException(e, "Problem with cognitive services");

                if (this.ShowDialogOnFaceApiErrors)
                {
                    await ErrorTrackingHelper.GenericApiCallExceptionHandler(e, "Problem with cognitive services");
                }
            }
            return facesInfo;
        }

        private static void FeedFaceInfo(Face f, FaceSendInfo fsi, Emotion e)
        {
            fsi.faceId = f.FaceId.ToString();
            fsi.age = f.FaceAttributes.Age;

            fsi.faceRecHeight = f.FaceRectangle.Height;
            fsi.faceRecLeft = f.FaceRectangle.Left;
            fsi.faceRecTop = f.FaceRectangle.Top;
            fsi.faceRecWidth = f.FaceRectangle.Width;

            fsi.gender = f.FaceAttributes.Gender;

            fsi.smile = f.FaceAttributes.Smile;

            fsi.beard = f.FaceAttributes.FacialHair.Beard;
            fsi.moustache = f.FaceAttributes.FacialHair.Moustache;
            fsi.sideburns = f.FaceAttributes.FacialHair.Sideburns;

            fsi.glasses = f.FaceAttributes.Glasses.ToString();

            fsi.headYaw = f.FaceAttributes.HeadPose.Yaw;
            fsi.headRoll = f.FaceAttributes.HeadPose.Roll;
            fsi.headPitch = f.FaceAttributes.HeadPose.Pitch;

            fsi.anger = e.Scores.Anger;
            fsi.contempt = e.Scores.Contempt;
            fsi.disgust = e.Scores.Disgust;
            fsi.fear = e.Scores.Fear;
            fsi.happiness = e.Scores.Happiness;
            fsi.neutral = e.Scores.Neutral;
            fsi.sadness = e.Scores.Sadness;
            fsi.surprise = e.Scores.Surprise;

            fsi.timeStamp = DateTime.Now;
        }

        private async Task CreatePrsonIfNoSimilarFaceExistsAsync(List<FaceSendInfo> facesInfo, Guid newPersonID, Face f)
        {
            //TODO return person result so we can change candidate if we are not going with the one selected

            //We try to find person also thru similar face api
            SimilarFaceMatch result = await GetSimilarFace(f);

            using (var db = new KioskDBContext())
            {
                //If we create new similar face, we create also new person
                if (result.isNew)
                {
                    //We create db where we map SimilarPersonID to PersonID, because those are different in cognitive services
                    var perResult = await CreatePerson(facesInfo, newPersonID, f);
                    db.SimilarFaces.Add(new DBSimilarFace() { CreatedAt = DateTime.Now, FaceId = result.SimilarPersistedFace.PersistedFaceId.ToString(), PersonId = perResult.ToString() });
                    await db.SaveChangesAsync();
                }

                else
                {
                    string personId = "";
                    try
                    {
                        personId = db.SimilarFaces.Where(sf => sf.FaceId == result.SimilarPersistedFace.PersistedFaceId.ToString()).FirstOrDefault().PersonId;

                        var person = await FaceServiceHelper.GetPersonAsync(groupId, new Guid(personId));

                        //Fill new info
                        var fi = facesInfo.Where(fin => fin.faceId == f.FaceId.ToString()).FirstOrDefault();
                        fi.canid = personId;
                        fi.canname = person.Name;
                        fi.canconf = result.SimilarPersistedFace.Confidence;

                        //We did not identified person thru person group and we needed to use silimar face api, so we add picture
                        await AddFaceToPerson(f, person, new Guid(personId));
                    }
                    catch (Exception)
                    {
                        //Person was not found due to old entry in local DB (Exception thrown by FaceApi GetPersonAsync) or face was created only in list when not using complex identification

                        //If person is only in list,  we need to create it also in person list
                        if(personId == "")
                        {
                            var perResult2 = await CreatePerson(facesInfo, newPersonID, f);
                            db.SimilarFaces.Add(new DBSimilarFace() { CreatedAt = DateTime.Now, FaceId = result.SimilarPersistedFace.PersistedFaceId.ToString(), PersonId = perResult2.ToString() });
                            await db.SaveChangesAsync();
                        }

                        //We clean the old entry from DB and create new Person
                        var oldDbEntry = db.SimilarFaces.Where(sf => sf.FaceId == result.SimilarPersistedFace.PersistedFaceId.ToString()).FirstOrDefault();
                        var perResult = await CreatePerson(facesInfo, newPersonID, f);
                        if (oldDbEntry != null)
                        {
                            db.SimilarFaces.Update(oldDbEntry);
                            oldDbEntry.FaceId = result.SimilarPersistedFace.PersistedFaceId.ToString();
                            oldDbEntry.PersonId = perResult.ToString();
                        }
                        else { 
                            db.SimilarFaces.Add(new DBSimilarFace() { CreatedAt = DateTime.Now, FaceId = result.SimilarPersistedFace.PersistedFaceId.ToString(), PersonId = perResult.ToString() });
                        }

                        await db.SaveChangesAsync();
                        
                    }
                }
            }
        }

        private async Task AddFaceToPerson( Face f, Person p, Guid personId)
        {
            //Maximum faces that we are able to persist
            if (p.PersistedFaceIds.Length == 248)
            {
                Guid persistedFaceId = p.PersistedFaceIds.OrderBy(x => Guid.NewGuid()).FirstOrDefault();
                await FaceServiceHelper.DeletePersonFaceAsync(groupId, personId, persistedFaceId);
            }
            try
            {
                await FaceServiceHelper.AddPersonFaceAsync(groupId, personId, await this.GetImageStreamCallback(), "", f.FaceRectangle);

            }
            catch (Exception e)
            {
                // Catch errors with individual groups so we can continue looping through all groups. Maybe an answer will come from
                // another one.
                ErrorTrackingHelper.TrackException(e, "Problem adding face to group");

                if (this.ShowDialogOnFaceApiErrors)
                {
                    await ErrorTrackingHelper.GenericApiCallExceptionHandler(e, "Problem adding face to group");
                }
            } 
        }

        private async Task<SimilarFaceMatch> GetSimilarFace(Face detectedFace)
        {
            SimilarFaceMatch result = new SimilarFaceMatch();
            try
            {
                Tuple<SimilarPersistedFace, string> similarPersistedFace = await FaceListManager.FindSimilarPersistedFaceAsync(await this.GetImageStreamCallback(), detectedFace.FaceId, detectedFace.FaceRectangle);
                if (similarPersistedFace != null)
                {
                    bool isNew = (similarPersistedFace.Item2 == null);                   
                    result = new SimilarFaceMatch(){ Face = detectedFace, SimilarPersistedFace = similarPersistedFace.Item1, isNew = isNew };
                }
            }
            catch (Exception e)
            {
                ErrorTrackingHelper.TrackException(e, "FaceListManager.FindSimilarPersistedFaceAsync error");

                if (this.ShowDialogOnFaceApiErrors)
                {
                    await ErrorTrackingHelper.GenericApiCallExceptionHandler(e, "Failure finding similar faces");
                }
            }       

            return result;
        }

        private async Task<Guid> CreatePerson(List<FaceSendInfo> facesInfo, Guid newPersonID, Face f)
        {
            AddPersistedFaceResult result = null;
            try
            {
                //No candidate we are going to create new person and set candidate to be same as newly created person with confidence of 100%
                var name = f.FaceAttributes.Gender + "-" + f.FaceAttributes.Age + "-" + newPersonID.ToString();

                newPersonID = (await FaceServiceHelper.CreatePersonWithResultAsync(groupId, name)).PersonId;
                var fi = facesInfo.Where(fin => fin.faceId == f.FaceId.ToString()).FirstOrDefault();
                fi.canid = newPersonID.ToString();
                fi.canname = name;
                fi.canconf = 1;
                
                await FaceServiceHelper.AddPersonFaceAsync(groupId, newPersonID, await this.GetImageStreamCallback(), "", f.FaceRectangle);

            }
            catch (Exception e)
            {
                // Catch errors with individual groups so we can continue looping through all groups. Maybe an answer will come from
                // another one.
                ErrorTrackingHelper.TrackException(e, "Problem adding face to group");

                if (this.ShowDialogOnFaceApiErrors)
                {
                    await ErrorTrackingHelper.GenericApiCallExceptionHandler(e, "Problem adding face to group");
                }
            }
            return newPersonID;
        }

        public async Task<IdentifyResult[]> IdentifyFacesAsync(string groupId)
        {
            this.IdentifiedPersons = Enumerable.Empty<IdentifiedPerson>();
            IdentifyResult[] groupResults = null;
            Guid[] detectedFaceIds = this.DetectedFaces?.Select(f => f.FaceId).ToArray();
            if (detectedFaceIds != null && detectedFaceIds.Any())
            {
                List<IdentifiedPerson> result = new List<IdentifiedPerson>();
               
                try {
                   
                    groupResults = await FaceServiceHelper.IdentifyAsync(groupId, detectedFaceIds);
                    foreach (var match in groupResults)
                    {
                        if (!match.Candidates.Any())
                        {
                            continue;
                        }
                        Person person = await FaceServiceHelper.GetPersonAsync(groupId, match.Candidates[0].PersonId);
                        result.Add(new IdentifiedPerson { Person = person, Confidence = match.Candidates[0].Confidence, FaceId = match.FaceId });
                    }
                }
                catch (Exception e)
                {
                    // Catch errors with individual groups so we can continue looping through all groups. Maybe an answer will come from
                    // another one.
                    ErrorTrackingHelper.TrackException(e, "Face API IdentifyAsync error");

                    //if (this.ShowDialogOnFaceApiErrors)
                    //{
                    //    await ErrorTrackingHelper.GenericApiCallExceptionHandler(e, "Failure identifying faces");
                    //}
                }
                this.IdentifiedPersons = result;
            }

            return groupResults;
        }


        public async Task FindSimilarPersistedFacesAsync()
        {
            this.SimilarFaceMatches = Enumerable.Empty<SimilarFaceMatch>();

            if (this.DetectedFaces == null || !this.DetectedFaces.Any())
            {
                return;
            }

            List<SimilarFaceMatch> result = new List<SimilarFaceMatch>();

            foreach (Face detectedFace in this.DetectedFaces)
            {
                try
                {
                    SimilarPersistedFace similarPersistedFace = (await FaceListManager.FindSimilarPersistedFaceAsync(await this.GetImageStreamCallback(), detectedFace.FaceId, detectedFace.FaceRectangle)).Item1;
                    if (similarPersistedFace != null)
                    {
                        result.Add(new SimilarFaceMatch { Face = detectedFace, SimilarPersistedFace = similarPersistedFace });
                    }
                }
                catch (Exception e)
                {
                    ErrorTrackingHelper.TrackException(e, "FaceListManager.FindSimilarPersistedFaceAsync error");

                    if (this.ShowDialogOnFaceApiErrors)
                    {
                        await ErrorTrackingHelper.GenericApiCallExceptionHandler(e, "Failure finding similar faces");
                    }
                }
            }

            this.SimilarFaceMatches = result;
        }

        private void OnFaceDetectionCompleted()
        {
            if (this.FaceDetectionCompleted != null)
            {
                this.FaceDetectionCompleted(this, EventArgs.Empty);
            }
        }

        private void OnFaceRecognitionCompleted()
        {
            if (this.FaceRecognitionCompleted != null)
            {
                this.FaceRecognitionCompleted(this, EventArgs.Empty);
            }
        }

        private void OnEmotionRecognitionCompleted()
        {
            if (this.EmotionRecognitionCompleted != null)
            {
                this.EmotionRecognitionCompleted(this, EventArgs.Empty);
            }
        }
    }


    public class IdentifiedPerson
    {
        public double Confidence
        {
            get; set;
        }

        public Person Person
        {
            get; set;
        }

        public Guid FaceId
        {
            get; set;
        }
    }

    public class SimilarFaceMatch
    {
        public Face Face
        {
            get; set;
        }

        public SimilarPersistedFace SimilarPersistedFace
        {
            get; set;
        }

        public bool isNew { get; set; }
    }


    public class FaceSendInfo
    {
        public string faceId { get; set; }

        public int faceRecWidth { get; set; }
        public int faceRecHeight { get; set; }
        public int faceRecLeft { get; set; }
        public int faceRecTop { get; set; }


        private double _age;
        public double age
        {
            get { return _age; }
            set { _age = Math.Round(value, 1); }
        }

        public string gender { get; set; }

        private double _headRoll;
        public double headRoll
        {
            get { return _headRoll; }
            set { _headRoll = Math.Round(value, 1); }
        }

        private double _headYaw;
        public double headYaw
        {
            get { return _headYaw; }
            set { _headYaw = Math.Round(value, 1); }
        }

        private double _headPitch;
        public double headPitch
        {
            get { return _headPitch; }
            set { _headPitch = Math.Round(value, 1); }
        }

        private double _smile;
        public double smile
        {
            get { return _smile; }
            set { _smile = Math.Round(value, 3); }
        }

        private double _moustache;
        public double moustache
        {
            get { return _moustache; }
            set { _moustache = Math.Round(value, 1); }
        }

        private double _beard;
        public double beard
        {
            get { return _beard; }
            set { _beard = Math.Round(value, 1); }
        }

        private double _sideburns;
        public double sideburns
        {
            get { return _sideburns; }
            set { _sideburns = Math.Round(value, 1); }
        }

        public string glasses { get; set; }

        private double _anger;
        public double anger
        {
            get { return _anger; }
            set { _anger = Math.Round(value, 3); }
        }

        private double _contempt;
        public double contempt
        {
            get { return _contempt; }
            set { _contempt = Math.Round(value, 3); }
        }

        private double _disgust;
        public double disgust
        {
            get { return _disgust; }
            set { _disgust = Math.Round(value, 3); }
        }

        private double _fear;
        public double fear
        {
            get { return _fear; }
            set { _fear = Math.Round(value, 3); }
        }

        private double _happiness;
        public double happiness
        {
            get { return _happiness; }
            set { _happiness = Math.Round(value, 3); }
        }

        private double _neutral;
        public double neutral
        {
            get { return _neutral; }
            set { _neutral = Math.Round(value, 3); }
        }

        private double _sadness;
        public double sadness
        {
            get { return _sadness; }
            set { _sadness = Math.Round(value, 3); }
        }

        private double _surprise;
        public double surprise
        {
            get { return _surprise; }
            set { _surprise = Math.Round(value, 3); }
        }

        public DateTime timeStamp { get; set; }

        public int facesNo { get; set; }

        public string canid { get; set; } = "";
        public string canname { get; set; } = "";
        public double canconf { get; set; } = 0;

        [JsonIgnore]
        public List<CandidateWithName> candidates { get; set; }

    }

    public class CandidateWithName
    {
        public string personId { get; set; }
        public string name { get; set; }
        public double confidence { get; set; }
    }

    public class IdentifiedFaces
    {
        public string Id { get; set; }
        public DateTime CreatedAt { get; set; }

        public IdentifiedFaces()
        {
            CreatedAt = DateTime.Now;
            NumberOfIdentifications = 1;
            NumOfAddedPhotosInLastPeriod = 1;
            FirstPhotoAddedInLastPeriod = DateTime.MinValue;
        }

        public int NumberOfIdentifications { get; set; }
        public Visibility Deleted { get; set; } = Visibility.Collapsed;

        public int NumOfAddedPhotosInLastPeriod { get; set; }

        public DateTime FirstPhotoAddedInLastPeriod { get; set; }
    }

}
