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

using Microsoft.ProjectOxford.Common;
using Microsoft.ProjectOxford.Emotion.Contract;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ServiceHelpers
{
    public class ImageAnalyzer
    {
        private static FaceAttributeType[] DefaultFaceAttributeTypes = new FaceAttributeType[] { FaceAttributeType.Age, FaceAttributeType.FacialHair, FaceAttributeType.Glasses, FaceAttributeType.Smile, FaceAttributeType.Gender, FaceAttributeType.HeadPose };

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

        public IdentifyResult Candidates { get; set; }

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

        public async Task<List<FaceSendInfo>> IdentifyOrAddPersonWithEmotionsAsync(string groupName, double confidence)
        {
            var time = DateTime.Now;
            //We also add emotions
            var facesInfo = new List<FaceSendInfo>();
            foreach (var f in this.DetectedFaces)
            {
                var fsi = new FaceSendInfo();
                var e = CoreUtil.FindEmotionForFace(f, this.DetectedEmotion);

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

                fsi.glasses = f.FaceAttributes.Glasses;

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
                
                fsi.timeStamp = time;
                fsi.facesNo = this.DetectedFaces.Count();

                facesInfo.Add(fsi);
            }
            var newPersonID = Guid.NewGuid();
            //Move to initialization
            try
            {
                await FaceServiceHelper.CreatePersonIfNoGroupExists(groupName, groupName);
            }
            catch (Exception ex)
            {
            }


            // Compute Face Identification and Unique Face Ids
            //We need to map detected faceID with actual faceID (Identified faceID)
            foreach (var f in this.DetectedFaces) {
                await Task.WhenAll(this.IdentifyFacesInSingleGroupAsync(groupName,f.FaceId));
                if (this.Candidates != null && this.Candidates.Candidates != null && this.Candidates.Candidates.Any())
                {
                    var candidates = new List<CandidateWithName>();
                    
                    //We need to get also name 
                    foreach(var can in this.Candidates.Candidates)
                    {
                        var p = await FaceServiceHelper.GetPersonAsync(groupName, can.PersonId);
                        CandidateWithName cwn = new CandidateWithName() { personId = p.PersonId.ToString(), confidence = can.Confidence, name = p.Name };
                        candidates.Add(cwn);
                    }
                    facesInfo.Where(i => i.faceId == f.FaceId.ToString()).FirstOrDefault().candidates = candidates;
                    //We have candidates
                    //For now we are returning all candidates
                    var c = this.Candidates.Candidates.OrderByDescending(ca => ca.Confidence).FirstOrDefault();
                    //foreach (var c in this.Candidates.Candidates.OrderByDescending(ca => ca.Confidence).For)
                    //{
                    //    //Check whether it has needed confidence
                    //    if (c.Confidence < confidence)
                    //    {
                    //        //if no we create person
                    //        newPersonID = (await FaceServiceHelper.CreatePersonWithResultAsync(groupName, Guid.NewGuid().ToString())).PersonId;
                    //        dfwes.Where(i => i.Face.FaceId == f.FaceId).FirstOrDefault().PersonId = newPersonID;
                    //        await FaceServiceHelper.AddPersonFaceAsync(groupName, newPersonID, await this.GetImageStreamCallback(), "", f.FaceRectangle);
                    //    }
                    if(c.Confidence >= confidence)
                    {
                        //We get person
                        Person pers = await FaceServiceHelper.GetPersonAsync(groupName, c.PersonId);
                        //dfwes.Where(i => i.Face.FaceId == f.FaceId).FirstOrDefault().PersonId = c.PersonId;
                        if (pers.PersistedFaceIds.Length == 248)
                        {
                            Guid persistedFaceId = pers.PersistedFaceIds.OrderBy(x => Guid.NewGuid()).FirstOrDefault();
                            await FaceServiceHelper.DeletePersonFaceAsync(groupName, c.PersonId, persistedFaceId);
                        }
                        await FaceServiceHelper.AddPersonFaceAsync(groupName, c.PersonId, await this.GetImageStreamCallback(), "", f.FaceRectangle);
                    }
                    //}
                }
                else
                {
                    //if no candidate we also need to create new person
                    try
                    {
                        //No candidate we are going to create new person
                        newPersonID = (await FaceServiceHelper.CreatePersonWithResultAsync(groupName, newPersonID.ToString())).PersonId;
                        //CandidateWithName c = new CandidateWithName() { confidence = 1, personId = newPersonID.ToString(), name = newPersonID.ToString() };
                        //facesInfo.Where(i => i.faceId == f.FaceId.ToString()).FirstOrDefault().candidates = new List<CandidateWithName>() {c};

                        await FaceServiceHelper.AddPersonFaceAsync(groupName, newPersonID, await this.GetImageStreamCallback(), "", f.FaceRectangle);
                    }
                    catch (Exception)
                    {}
                }
                try
                {
                    await FaceServiceHelper.TrainPersonGroupAsync(groupName);
                }
                catch (Exception)
                {}
            }
            
            return facesInfo;
        }

        public async Task IdentifyFacesInSingleGroupAsync(string personGroupName, Guid faceId)
        {
            this.IdentifiedPersons = Enumerable.Empty<IdentifiedPerson>();

            Guid[] detectedFaceIds = new Guid[] { faceId };

            try {
                if (detectedFaceIds != null && detectedFaceIds.Any())
                {
                    List<IdentifiedPerson> result = new List<IdentifiedPerson>();
                    PersonGroup personGroup = null;

                    try
                    {
                        var groups = await FaceServiceHelper.GetPersonGroupsAsync();
                        personGroup = groups.Where(g => g.PersonGroupId == personGroupName).FirstOrDefault();
                    }
                    catch (Exception e)
                    {
                        ErrorTrackingHelper.TrackException(e, "Face API GetPersonGroupsAsync error");

                        if (this.ShowDialogOnFaceApiErrors)
                        {
                            await ErrorTrackingHelper.GenericApiCallExceptionHandler(e, "Failure getting PersonGroup");
                        }
                    }

                    if (personGroup != null)
                    {
                        this.Candidates = (await FaceServiceHelper.IdentifyAsync(personGroup.PersonGroupId, detectedFaceIds)).FirstOrDefault();
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

            this.OnFaceRecognitionCompleted();
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
                    SimilarPersistedFace similarPersistedFace = await FaceListManager.FindSimilarPersistedFaceAsync(await this.GetImageStreamCallback(), detectedFace.FaceId, detectedFace.FaceRectangle);
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

        public Glasses glasses { get; set; }

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

        public List<CandidateWithName> candidates { get; set; }

    }

    public class CandidateWithName
    {
        public string personId { get; set; }
        public string name { get; set; }
        public double confidence { get; set; }
    }

}
