﻿// 
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
using Microsoft.ProjectOxford.Face;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ProjectOxford.Face.Contract;
using Microsoft.ProjectOxford.Emotion.Contract;

namespace ServiceHelpers
{
    public class CoreUtil
    {
        public static uint MinDetectableFaceCoveragePercentage = 0;

        public static bool IsFaceBigEnoughForDetection(int faceHeight, int imageHeight)
        {
            if (imageHeight == 0)
            {
                // sometimes we don't know the size of the image, so we assume the face is big enough
                return true;
            }

            double faceHeightPercentage = 100 * ((double)faceHeight / imageHeight);

            return faceHeightPercentage >= MinDetectableFaceCoveragePercentage;
        }

        public static Emotion FindFaceClosestToRegion(IEnumerable<Emotion> emotion, FaceRectangle region)
        {
            return emotion?.Where(e => CoreUtil.AreFacesPotentiallyTheSame(e.FaceRectangle, region))
                                  .OrderBy(e => Math.Abs(region.Left - e.FaceRectangle.Left) + Math.Abs(region.Top - e.FaceRectangle.Top)).FirstOrDefault();
        }

        public static bool AreFacesPotentiallyTheSame(Rectangle face1, FaceRectangle face2)
        {
            return AreFacesPotentiallyTheSame((int)face1.Left, (int)face1.Top, (int)face1.Width, (int)face1.Height, face2.Left, face2.Top, face2.Width, face2.Height);
        }

        public static bool AreFacesPotentiallyTheSame(int face1X, int face1Y, int face1Width, int face1Height,
                                                       int face2X, int face2Y, int face2Width, int face2Height)
        {
            double distanceThresholdFactor = 1;
            double sizeThresholdFactor = 0.5;

            // See if faces are close enough from each other to be considered the "same"
            if (Math.Abs(face1X - face2X) <= face1Width * distanceThresholdFactor &&
                Math.Abs(face1Y - face2Y) <= face1Height * distanceThresholdFactor)
            {
                // See if faces are shaped similarly enough to be considered the "same"
                if (Math.Abs(face1Width - face2Width) <= face1Width * sizeThresholdFactor &&
                    Math.Abs(face1Height - face2Height) <= face1Height * sizeThresholdFactor)
                {
                    return true;
                }
            }

            return false;
        }

        public static Emotion FindEmotionForFace(Face f, IEnumerable<Emotion> emotions)
        {
            foreach(var e in emotions)
            {
                if(CompareRectangles(f.FaceRectangle, e.FaceRectangle, 5))
                {
                    return e;
                }
            }
            return null;
        }

        public static bool CompareRectangles(FaceRectangle r1, Rectangle r2, int benevolence)
        {
            
            if(Math.Abs(r1.Height - r2.Height) > benevolence)
                return false;
            if (Math.Abs(r1.Width - r2.Width) > benevolence)
                return false;
            if (Math.Abs(r1.Left - r2.Left) > benevolence)
                return false;
            if (Math.Abs(r1.Top - r2.Top) > benevolence)
                return false;

            return true;
        }

    }

    public class DetectedAndIdentifiedFaceWithEmotions
    {
        public Emotion Emotion { get; set; }
        public Face Face { get; set; }

        public List<Candidate> Candidates {get; set;}

       // public Guid PersonId { get; set; }

        public DateTime TimeStamp { get; set; }
    }

}
