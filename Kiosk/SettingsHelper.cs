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

using System;
using System.ComponentModel;
using System.IO;
using Windows.Storage;

namespace IntelligentKioskSample
{
    internal class SettingsHelper : INotifyPropertyChanged
    {
        public event EventHandler SettingsChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        private static SettingsHelper instance;

        static SettingsHelper()
        {
            instance = new SettingsHelper();
        }

        public void Initialize()
        {
            LoadRoamingSettings();
            Windows.Storage.ApplicationData.Current.DataChanged += RoamingDataChanged;
        }

        private void RoamingDataChanged(ApplicationData sender, object args)
        {
            LoadRoamingSettings();
            instance.OnSettingsChanged();
        }

        private void OnSettingsChanged()
        {
            if (instance.SettingsChanged != null)
            {
                instance.SettingsChanged(instance, EventArgs.Empty);
            }
        }

        private async void OnSettingChanged(string propertyName, object value)
        {
            ApplicationData.Current.RoamingSettings.Values[propertyName] = value;
            
            instance.OnSettingsChanged();
            instance.OnPropertyChanged(propertyName);
        }

        private void OnPropertyChanged(string propertyName)
        {
            if (instance.PropertyChanged != null)
            {
                instance.PropertyChanged(instance, new PropertyChangedEventArgs(propertyName));
            }
        }

        public static SettingsHelper Instance
        {
            get
            {
                return instance;
            }
        }

        private async void LoadRoamingSettings()
        {
            object value = ApplicationData.Current.RoamingSettings.Values["FaceApiKey"];
            if (value != null)
            {
                this.FaceApiKey = value.ToString();
            }

            value = ApplicationData.Current.RoamingSettings.Values["EmotionApiKey"];
            if (value != null)
            {
                this.EmotionApiKey = value.ToString();
            }

            value = ApplicationData.Current.RoamingSettings.Values["Confidence"];
            if (value != null)
            {
                this.Confidence = Convert.ToDouble(value);
            }

            value = ApplicationData.Current.RoamingSettings.Values["CameraName"];
            if (value != null)
            {
                this.CameraName = value.ToString();
            }

            value = ApplicationData.Current.RoamingSettings.Values["PhotoFrequency"];
            if (value != null)
            {
                this.PhotoFrequency = Convert.ToInt32(value);
            }

            value = ApplicationData.Current.RoamingSettings.Values["DeleteWindow"];
            if (value != null)
            {
                this.DeleteWindow = Convert.ToInt32(value);
            }

            value = ApplicationData.Current.RoamingSettings.Values["GroupName"];
            if (value != null)
            {
                this.GroupName = value.ToString();
            }

            value = ApplicationData.Current.RoamingSettings.Values["HubNamespace"];
            if (value != null)
            {
                this.HubNamespace = value.ToString();
            }

            value = ApplicationData.Current.RoamingSettings.Values["HubName"];
            if (value != null)
            {
                this.HubName = value.ToString();
            }

            value = ApplicationData.Current.RoamingSettings.Values["HubKey"];
            if (value != null)
            {
                this.HubKey = value.ToString();
            }



            value = ApplicationData.Current.RoamingSettings.Values["MinDetectableFaceCoveragePercentage"];
            if (value != null)
            {
                uint size;
                if (uint.TryParse(value.ToString(), out size))
                {
                    this.MinDetectableFaceCoveragePercentage = size;
                }
            }

            value = ApplicationData.Current.RoamingSettings.Values["ShowDebugInfo"];
            if (value != null)
            {
                bool booleanValue;
                if (bool.TryParse(value.ToString(), out booleanValue))
                {
                    this.ShowDebugInfo = booleanValue;
                }
            }

            value = ApplicationData.Current.RoamingSettings.Values["ComplexIdentification"];
            if (value != null)
            {
                bool booleanValue;
                if (bool.TryParse(value.ToString(), out booleanValue))
                {
                    this.ComplexIdentification = booleanValue;
                }
            }
        }


        public void RestoreAllSettings()
        {
            ApplicationData.Current.RoamingSettings.Values.Clear();
        }

        private string faceApiKey = string.Empty;
        public string FaceApiKey
        {
            get { return this.faceApiKey; }
            set
            {
                this.faceApiKey = value;
                this.OnSettingChanged("FaceApiKey", value);
            }
        }


        private string groupName = "group";
        public string GroupName
        {
            get { return this.groupName; }
            set
            {
                this.groupName = value;
                this.OnSettingChanged("GroupName", value);
            }
        }

        private string emotionApiKey = string.Empty;
        public string EmotionApiKey
        {
            get { return this.emotionApiKey; }
            set
            {
                this.emotionApiKey = value;
                this.OnSettingChanged("EmotionApiKey", value);
            }
        }


        private int photoFrequency = 1;
        public int PhotoFrequency
        {
            get { return photoFrequency; }
            set
            {
                this.photoFrequency = value;
                this.OnSettingChanged("PhotoFrequency", value);
            }
        }

        //specifies how much time we leave before we delete face with only one identification (in seconds)
        private int deleteWindow = 30;
        public int DeleteWindow
        {
            get { return deleteWindow; }
            set
            {
                this.deleteWindow = value;
                this.OnSettingChanged("DeleteWindow", value);
            }
        }

        private double confidence = 0.8;
        public double Confidence
        {
            get { return confidence; }
            set
            {
                this.confidence = value;
                this.OnSettingChanged("Confidence", value);
            }
        }


        private string cameraName = string.Empty;
        public string CameraName
        {
            get { return cameraName; }
            set
            {
                this.cameraName = value;
                this.OnSettingChanged("CameraName", value);
            }
        }

        private uint minDetectableFaceCoveragePercentage = 7;
        public uint MinDetectableFaceCoveragePercentage
        {
            get { return this.minDetectableFaceCoveragePercentage; }
            set
            {
                this.minDetectableFaceCoveragePercentage = value;
                this.OnSettingChanged("MinDetectableFaceCoveragePercentage", value);
            }
        }

        private bool showDebugInfo = false;
        public bool ShowDebugInfo
        {
            get { return showDebugInfo; }
            set
            {
                this.showDebugInfo = value;
                this.OnSettingChanged("ShowDebugInfo", value);
            }
        }


        private string hubNamespace = "kioskeventhub";
        public string HubNamespace
        {
            get { return hubNamespace; }
            set
            {
                this.hubNamespace = value;
                this.OnSettingChanged("HubNamespace", value);
            }
        }

        private string hubName = "hub1";
        public string HubName
        {
            get { return hubName; }
            set
            {
                this.hubName = value;
                this.OnSettingChanged("HubName", value);
            }
        }

        private string hubKey = "tvzonqLkFUm+9AXlT/rq8Fmh0HlzND7MwOiGW6r0TNo=";
        public string HubKey
        {
            get { return hubKey; }
            set
            {
                this.hubKey = value;
                this.OnSettingChanged("HubKey", value);
            }
        }

        private string hubKeyName = "AllowSend";
        public string HubKeyName
        {
            get { return hubKeyName; }
            set
            {
                this.hubKeyName = value;
                this.OnSettingChanged("HubKeyName", value);
            }
        }

        /// <summary>
        /// How many faces do we need the person to have, so we do not delete and mark as inaccurately learned
        /// </summary>
        private int neededFaceIdentNum = 3;
        public int NeededFaceIdentNum
        {
            get { return neededFaceIdentNum; }
            set
            {
                this.neededFaceIdentNum = value;
                this.OnSettingChanged("NeededFaceIdentNum", value);
            }
        }

        /// <summary>
        /// Number of photos we add in one period (understand in one session in front of cammera)
        /// </summary>
        private int numberOfPhotoAddsInPeriod = 5;
        public int NumberOfPhotoAddsInPeriod
        {
            get { return numberOfPhotoAddsInPeriod; }
            set
            {
                this.numberOfPhotoAddsInPeriod = value;
                this.OnSettingChanged("NumberOfPhotoAddsInPeriod", value);
            }
        }

        /// <summary>
        /// Period (in hours) which we take as one session in front of cammera (one visit of space where the kiosk is placed), it is not probable, that person will change his or her face landmarks radically
        /// </summary>
        private int photoAddPeriodSize = 1;
        public int PhotoAddPeriodSize
        {
            get { return photoAddPeriodSize; }
            set
            {
                this.photoAddPeriodSize = value;
                this.OnSettingChanged("PhotoAddPeriodSize", value);
            }
        }


        /// <summary>
        ///Do we use complex identification or not
        /// </summary>
        private bool complexIdentification = true;
        public bool ComplexIdentification
        {
            get { return complexIdentification; }
            set
            {
                this.complexIdentification = value;
                this.OnSettingChanged("ComplexIdentification", value);
            }
        }



    }
}