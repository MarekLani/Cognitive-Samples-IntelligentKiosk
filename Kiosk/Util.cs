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

using ServiceHelpers;
using Microsoft.ProjectOxford.Common;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Popups;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using ppatierno.AzureSBLite;
using ppatierno.AzureSBLite.Messaging;
using System.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Net.Http;

namespace IntelligentKioskSample
{
    internal static class Util
    {
        public static string CapitalizeString(string s)
        {
            return string.Join(" ", s.Split(' ').Select(word => !string.IsNullOrEmpty(word) ? char.ToUpper(word[0]) + word.Substring(1) : string.Empty));
        }

        internal static async Task GenericApiCallExceptionHandler(Exception ex, string errorTitle)
        {
            string errorDetails = GetMessageFromException(ex);

            await new MessageDialog(errorDetails, errorTitle).ShowAsync();
        }

        internal static string GetMessageFromException(Exception ex)
        {
            string errorDetails = ex.Message;

            FaceAPIException faceApiException = ex as FaceAPIException;
            if (faceApiException?.ErrorMessage != null)
            {
                errorDetails = faceApiException.ErrorMessage;
            }

            Microsoft.ProjectOxford.Common.ClientException commonException = ex as Microsoft.ProjectOxford.Common.ClientException;
            if (commonException?.Error?.Message != null)
            {
                errorDetails = commonException.Error.Message;
            }

            return errorDetails;
        }

        internal static Face FindFaceClosestToRegion(IEnumerable<Face> faces, BitmapBounds region)
        {
            return faces?.Where(f => Util.AreFacesPotentiallyTheSame(region, f.FaceRectangle))
                                  .OrderBy(f => Math.Abs(region.X - f.FaceRectangle.Left) + Math.Abs(region.Y - f.FaceRectangle.Top)).FirstOrDefault();
        }

        internal static bool AreFacesPotentiallyTheSame(BitmapBounds face1, FaceRectangle face2)
        {
            return CoreUtil.AreFacesPotentiallyTheSame((int)face1.X, (int)face1.Y, (int)face1.Width, (int)face1.Height, face2.Left, face2.Top, face2.Width, face2.Height);
        }

        public static async Task ConfirmActionAndExecute(string message, Func<Task> action)
        {
            var messageDialog = new MessageDialog(message);

            messageDialog.Commands.Add(new UICommand("Yes", new UICommandInvokedHandler(async (c) => await action())));
            messageDialog.Commands.Add(new UICommand("Cancel", new UICommandInvokedHandler((c) => { })));

            messageDialog.DefaultCommandIndex = 1;
            messageDialog.CancelCommandIndex = 1;

            await messageDialog.ShowAsync();
        }

        public static async Task<IEnumerable<string>> GetAvailableCameraNamesAsync()
        {
            DeviceInformationCollection deviceInfo = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            return deviceInfo.OrderBy(d => d.Name).Select(d => d.Name);
        }

        async public static Task<ImageSource> GetCroppedBitmapAsync(Func<Task<Stream>> originalImgFile, Microsoft.ProjectOxford.Common.Rectangle rectangle)
        {
            try
            {
                using (IRandomAccessStream stream = (await originalImgFile()).AsRandomAccessStream())
                {
                    return await GetCroppedBitmapAsync(stream, rectangle);
                }
            }
            catch
            {
                // default to no image if we fail to crop the bitmap
                return null;
            }
        }

        async public static Task<ImageSource> GetCroppedBitmapAsync(IRandomAccessStream stream, Microsoft.ProjectOxford.Common.Rectangle rectangle)
        {
            var pixels = await GetCroppedPixelsAsync(stream, rectangle);

            // Stream the bytes into a WriteableBitmap 
            WriteableBitmap cropBmp = new WriteableBitmap(rectangle.Width, rectangle.Height);
            cropBmp.FromByteArray(pixels);

            return cropBmp;
        }

        async private static Task<byte[]> GetCroppedPixelsAsync(IRandomAccessStream stream, Rectangle rectangle)
        {
            // Create a decoder from the stream. With the decoder, we can get  
            // the properties of the image. 
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

            // Create cropping BitmapTransform and define the bounds. 
            BitmapTransform transform = new BitmapTransform();
            BitmapBounds bounds = new BitmapBounds();
            bounds.X = (uint)rectangle.Left;
            bounds.Y = (uint)rectangle.Top;
            bounds.Height = (uint)rectangle.Height;
            bounds.Width = (uint)rectangle.Width;
            transform.Bounds = bounds;

            // Get the cropped pixels within the bounds of transform. 
            PixelDataProvider pix = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                transform,
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.ColorManageToSRgb);

            return pix.DetachPixelData();
        }

		internal static async Task<byte[]> GetPixelBytesFromSoftwareBitmapAsync(SoftwareBitmap softwareBitmap)
		{
			using (InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream())
			{
				BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
				encoder.SetSoftwareBitmap(softwareBitmap);
				await encoder.FlushAsync();

				// Read the pixel bytes from the memory stream
				using (var reader = new DataReader(stream.GetInputStreamAt(0)))
				{
					var bytes = new byte[stream.Size];
					await reader.LoadAsync((uint)stream.Size);
					reader.ReadBytes(bytes);
					return bytes;
				}
			}
		}

        private static MessagingFactory factory = null;
        private static EventHubClient client = null;

        internal static void SendMessageToEventHub(string payload)
        {
            string connectionString= "Endpoint=sb://kioskeventhub.servicebus.windows.net/;SharedAccessKeyName=AllowSend;SharedAccessKey=tvzonqLkFUm+9AXlT/rq8Fmh0HlzND7MwOiGW6r0TNo=";
            ServiceBusConnectionStringBuilder builder = new ServiceBusConnectionStringBuilder(connectionString);
            builder.TransportType = TransportType.Amqp;

            if(factory == null)
                factory = MessagingFactory.CreateFromConnectionString(connectionString);
            if(client == null)
                client = factory.CreateEventHubClient("hub1");

            EventHubSender sender = client.CreatePartitionedSender("partition");

            EventData data = new EventData(Encoding.UTF8.GetBytes(payload));
            data.Properties["time"] = DateTime.UtcNow;
            sender.Send(data);
            sender.Close();
            
        }

        public static void AddRange<T>(this IList<T> list, IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                list.Add(item);
            }
        }
        private static string sasToken = null;
        public static async Task CallEventHubHttp(string payload)
        {

            var serviceNamespace = SettingsHelper.Instance.HubNamespace;
            var hubName = SettingsHelper.Instance.HubName;
            var baseAddress = new Uri(string.Format("https://{0}.servicebus.windows.net/", serviceNamespace));
            var url = baseAddress + string.Format("{0}/publishers/testdevice/messages", hubName);

            // Create client.
            var httpClient = new HttpClient();

            if(sasToken == null)
                sasToken = createToken(serviceNamespace, SettingsHelper.Instance.HubKeyName, SettingsHelper.Instance.HubKey);

            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", sasToken);

            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            content.Headers.Add("ContentType", "application/json");
            try
            {
                await httpClient.PostAsync(url, content);
            }
            catch(Exception ex)
            {
                sasToken = ex.Message;
                sasToken = createToken(serviceNamespace, "AllowSend", "tvzonqLkFUm+9AXlT/rq8Fmh0HlzND7MwOiGW6r0TNo=");
            }
        }

        private static string createToken(string resourceUri, string keyName, string key)
        {
            TimeSpan sinceEpoch = DateTime.UtcNow - new DateTime(1970, 1, 1);
            var week = 60 * 60 * 24 * 7;
            var expiry = Convert.ToString((int)sinceEpoch.TotalSeconds + week);
            string stringToSign =  Uri.EscapeDataString(resourceUri) + "\n" + expiry;
            HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
            var sasToken = String.Format(CultureInfo.InvariantCulture, "SharedAccessSignature sr={0}&sig={1}&se={2}&skn={3}", Uri.EscapeDataString(resourceUri), Uri.EscapeDataString(signature), expiry, keyName);
            return sasToken;
        }

        public static void SendAMQPMessage(string payload)
        {
            string eventHubNamespace = "kioskeventhub";
            string eventHubName = "hub1";
            string policyName = "AllowSend";
            string key = "tvzonqLkFUm+9AXlT/rq8Fmh0HlzND7MwOiGW6r0TNo=";
            string partitionkey = "partitionkey";

            //Endpoint = sb://kioskeventhub.servicebus.windows.net/;SharedAccessKeyName=SendPolicy;SharedAccessKey=/ncGKkouu7a4qVdEHPgwdmBv5p2fGDGf7aPHjD0/5n8=;EntityPath=hub1

            string data = payload;

            Amqp.Address address = new Amqp.Address(
                string.Format("{0}.servicebus.windows.net", eventHubNamespace),
                5671, policyName, key);

            Amqp.Connection connection = new Amqp.Connection(address);
            Amqp.Session session = new Amqp.Session(connection);
            Amqp.SenderLink senderlink = new Amqp.SenderLink(session,
                string.Format("send-link:{0}", eventHubName), eventHubName);

            Amqp.Message message = new Amqp.Message()
            {
                BodySection = new Amqp.Framing.Data()
                {
                    Binary = System.Text.Encoding.UTF8.GetBytes(data)
                }
            };

            message.MessageAnnotations = new Amqp.Framing.MessageAnnotations();
            message.MessageAnnotations[new Amqp.Types.Symbol("x-opt-partition-key")] =
               string.Format("pk:", partitionkey);

            senderlink.Send(message);
        }
    }
}
