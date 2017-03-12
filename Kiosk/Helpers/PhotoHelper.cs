using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;

namespace ServiceHelpers
{
    public class PhotoHelper
    { 
        public static MediaCapture camera = null;

        public static async Task<Stream> TakePhoto()
        {
            if (camera != null)
                return await Capture();
            else
                return null;
        }

       
        private static async Task SetMaxResolution(MediaCapture mediaCapture)
        {
            IReadOnlyList<IMediaEncodingProperties> res =
                mediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoPreview);
            uint maxResolution = 0;
            int indexMaxResolution = 0;

            if (res.Count >= 1)
            {
                for (int i = 0; i < res.Count; i++)
                {
                    VideoEncodingProperties vp = (VideoEncodingProperties)res[i];

                    if (vp.Width <= maxResolution) continue;
                    indexMaxResolution = i;
                    maxResolution = vp.Width;
                }
                await
                    mediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview,
                        res[indexMaxResolution]);
            }
        }

        private static async Task<Tuple<BitmapDecoder, IRandomAccessStream>> GetPhotoStreamAsync(
            MediaCapture mediaCapture)
        {
            InMemoryRandomAccessStream photoStream = new InMemoryRandomAccessStream();
            await mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), photoStream);
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(photoStream);
            return new Tuple<BitmapDecoder, IRandomAccessStream>(decoder, photoStream.CloneStream());
        }

        private static async Task<Stream> Capture()
        {

            Debug.WriteLine($"Processing camera");
            BitmapDecoder bitmapDecoder;
            IRandomAccessStream imageStream;
            try
            {
                Tuple<BitmapDecoder, IRandomAccessStream> photoData = await GetPhotoStreamAsync(camera);
                bitmapDecoder = photoData.Item1;
                imageStream = photoData.Item2;

                Debug.WriteLine($"Got stream from camera");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Camera  failed with message: {ex.Message}");
                return null;
            }
#pragma warning disable 4014

            return  ProcessImage(bitmapDecoder, imageStream);
#pragma warning restore 4014
        }



        private static Stream ProcessImage(BitmapDecoder bitmapDecoder, IRandomAccessStream imageStream)
        {
            try
            {
                MemoryStream imageMemoryStream = new MemoryStream();
                //var image = new ImageProcessorCore.Image(imageStream.AsStreamForRead());
                //image.SaveAsJpeg(imageMemoryStream, 80);

                //return imageMemoryStream;
                return imageStream.AsStreamForRead();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Exception when processingImage: {e.Message}");
                return null;
            }
        }
    }
}
