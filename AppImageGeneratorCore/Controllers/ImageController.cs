//using System;
//using System.Collections.Generic;
//using System.Drawing;
//using System.Drawing.Drawing2D;
//using System.Drawing.Imaging;
//using System.IO;
//using System.Linq;
//using System.Net;
//using System.Net.Http;
//using System.Net.Http.Headers;
//using System.Runtime.Serialization;
//using System.Threading.Tasks;
//using System.Web;
//using System.Web.Http;
//using Ionic.Zip;
//using Newtonsoft.Json;
//using Svg;
//using Svg.Transforms;
using System.IO.Compression;

using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using Newtonsoft.Json;

namespace WWA.WebUI.Controllers
{
    public class InputModel
    {
        public IFormFile MyProperty { get; set; }
        public double Padding { get; set; }
        public string BackgroundColor { get; set; }
    }

    public class Profile
    {
        public int Width { get; set; }

        public int Height { get; set; }

        public string Name { get; set; }

        public string Desc { get; set; }

        public string Folder { get; set; }

        public string Format { get; set; }
    }

    [Route("api/image")]
    public class ImageController : Controller
    {
        public IActionResult Get(string id)
        {
            try
            {
                // Create path from the id and return the file...
                //                string zipFilePath = CreateFilePathFromId(new Guid(id));
                //                if (string.IsNullOrEmpty(zipFilePath))
                //                {
                //                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, string.Format("Can't find {0}", id));
                //                }

                //                httpResponseMessage = Request.CreateResponse();
                //                httpResponseMessage.Content = new ByteArrayContent(File.ReadAllBytes(zipFilePath));
                //                httpResponseMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                //                httpResponseMessage.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                //                {
                //                    FileName = "AppImages.zip"
                //                };

                //                File.Delete(zipFilePath);
            }
            catch (Exception ex)
            {
                return new StatusCodeResult((int)HttpStatusCode.InternalServerError);
                //httpResponseMessage = Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }

            //            return httpResponseMessage;
        }

        private static string ReadStringFromConfigFile(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (var sr = new StreamReader(fs))
            {
                return sr.ReadToEnd();
            }
        }

        //        private IEnumerable<string> GetConfig(string platformId)
        //        {
        //            List<string> config = new List<string>();
        //            string root = HttpContext.Current.Server.MapPath("~/App_Data");
        //            string filePath = Path.Combine(root, platformId + "Images.json");
        //            config.Add(ReadStringFromConfigFile(filePath));
        //            return config;
        //        }

        // POST api/image
        public async Task<IActionResult> Post([FromBody]InputModel model)
        {
            string root = HttpContext.Current.Server.MapPath("~/App_Data");
            var provider = new MultipartFormDataStreamProvider(root);
            Guid zipId = Guid.NewGuid();

            try
            {

                // Read the form data.
                var sourceImage = new object();

                if (model.MyProperty.ContentType == "whateversvgis")
                {

                }
                else
                {
                    //regular image scenario
                }


                MultipartFileData multipartFileData = provider.FileData.First();

                using (var model = new IconModel())
                {
                    var ct = multipartFileData.Headers.ContentType.MediaType;
                    if (ct != null && ct.Contains("svg"))
                    {
                        model.SvgFile = multipartFileData.LocalFileName;
                    }
                    else
                    {
                        model.InputImage = Image.FromFile(multipartFileData.LocalFileName);
                    }
                    model.Padding = Convert.ToDouble(provider.FormData.GetValues("padding")[0]);
                    if (model.Padding < 0 || model.Padding > 1.0)
                    {
                        // Throw out as user has supplied invalid hex string..
                        return BadRequest("Padding value invalid. Please input a number between 0 and 1.");
                    }

                    var colorStr = provider.FormData.GetValues("color")?[0];
                    var colorChanged = provider.FormData.GetValues("colorChanged")?[0] == "1";

                    if (!string.IsNullOrEmpty(colorStr) && colorChanged)
                    {
                        try
                        {
                            var colorConverter = new ColorConverter();
                            model.Background = (Color)colorConverter.ConvertFromString(colorStr);
                        }
                        catch (Exception ex)
                        {
                            // Throw out as user has supplied invalid hex string..
                            return BadRequest("Background Color value invalid. Please input a valid hex color.");
                        }
                    }

                    var platforms = provider.FormData.GetValues("platform");

                    if (platforms == null)
                    {
                        // Throw out as user has supplied no platforms..
                        return BadRequest("No platform has been specified.");
                    }

                    model.Platforms = platforms;

                    List<Profile> profiles = null;

                    foreach (var platform in model.Platforms)
                    {
                        // Get the platform and profiles
                        IEnumerable<string> config = GetConfig(platform);
                        if (config.Count() < 1)
                        {
                            return BadRequest();
                        }

                        foreach (var cfg in config)
                        {
                            if (profiles == null)
                                profiles = JsonConvert.DeserializeObject<List<Profile>>(cfg);
                            else
                                profiles.AddRange(JsonConvert.DeserializeObject<List<Profile>>(cfg));
                        }
                    }

                    using (var memory = new MemoryStream())
                    using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
                    {
                        var iconObject = new IconRootObject();
                        foreach (var profile in profiles)
                        {
                            string fmt = string.IsNullOrEmpty(profile.Format) ? "png" : profile.Format;
                            var zipFile = archive.CreateEntry(profile.Folder + profile.Name + "." + fmt);

                            MemoryStream stream = CreateImageStream(model, profile);


                            using (var fileStream = zipFile.Open())
                            {
                                stream.CopyTo(fileStream);
                            }


                            iconObject.icons.Add(new IconObject(profile.Folder + profile.Name + "." + fmt, profile.Width + "x" + profile.Height));


                        }

                        using (var fileStream = archive.CreateEntry("icons.json").Open())
                        {
                            var iconStr = JsonConvert.SerializeObject(iconObject, Formatting.Indented);
                            using (var sw = new StreamWriter(fileStream))
                            {
                                sw.Write(iconStr);
                            }
                        }



                        string zipFilePath = CreateFilePathFromId(zipId);
                        zip.Save(zipFilePath);
                    }
                }

                // Delete source image file from local disk
                File.Delete(multipartFileData.LocalFileName);
            }
            catch (OutOfMemoryException ex)
            {
                return StatusCode((int)HttpStatusCode.UnsupportedMediaType);
            }
            catch (Exception ex)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError);
            }

            var url = Url.RouteUrl("DefaultApi", new { controller = "image", id = zipId.ToString() });

            var uri = new Uri(url, UriKind.Relative);
            return Created(uri, null);

        }

        private static Stream CreateImageStream(IconModel model, Profile profile)
        {
            if (model.SvgFile != null)
            {
                return RenderSvgToStream(model.SvgFile, profile.Width, profile.Height, profile.Format, model.Padding, model.Background);
            }
            else
            {
                return ResizeImage(model.InputImage, profile.Width, profile.Height, profile.Format, model.Padding, model.Background);
            }
        }

        private static Stream RenderSvgToStream(string filename, int width, int height, string fmt, double paddingProp = 0.3, Color? bg = null)
        {
            var displaySize = new Size(width, height);

            SvgDocument svgDoc = SvgDocument.Open(filename);
            RectangleF svgSize = RectangleF.Empty;
            try
            {
                svgSize.Width = svgDoc.GetDimensions().Width;
                svgSize.Height = svgDoc.GetDimensions().Height;
            }
            catch (Exception ex)
            { }

            if (svgSize == RectangleF.Empty)
            {
                svgSize = new RectangleF(0, 0, svgDoc.ViewBox.Width, svgDoc.ViewBox.Height);
            }

            if (svgSize.Width == 0)
            {
                throw new Exception("SVG does not have size specified. Cannot work with it.");
            }

            var displayProportion = (displaySize.Height * 1.0f) / displaySize.Width;
            var svgProportion = svgSize.Height / svgSize.Width;

            float scalingFactor = 0f;
            int padding = 0;

            // if display is proportionally narrower than svg
            if (displayProportion > svgProportion)
            {
                padding = (int)(paddingProp * width * 0.5);
                // we pick the width of display as max and compute the scaling against that.
                scalingFactor = ((displaySize.Width - padding * 2) * 1.0f) / svgSize.Width;
            }
            else
            {
                padding = (int)(paddingProp * height * 0.5);
                // we pick the height of display as max and compute the scaling against that.
                scalingFactor = ((displaySize.Height - padding * 2) * 1.0f) / svgSize.Height;
            }

            if (scalingFactor < 0)
            {
                throw new Exception("Viewing area is too small to render the image");
            }

            // When proportions of drawing do not match viewing area, it's nice to center the drawing within the viewing area.
            int centeringX = Convert.ToInt16((displaySize.Width - (padding + svgDoc.Width * scalingFactor)) / 2);
            int centeringY = Convert.ToInt16((displaySize.Height - (padding + svgDoc.Height * scalingFactor)) / 2);

            // Remove the "+ centering*" to avoid growing and padding the Bitmap with transparent fill.
            svgDoc.Transforms = new SvgTransformCollection();
            svgDoc.Transforms.Add(new SvgTranslate(padding + centeringX, padding + centeringY));
            svgDoc.Transforms.Add(new SvgScale(scalingFactor));

            // This keeps the size of bitmap fixed to stated viewing area. Image is padded with transparent areas.
            svgDoc.Width = new SvgUnit(svgDoc.Width.Type, displaySize.Width);
            svgDoc.Height = new SvgUnit(svgDoc.Height.Type, displaySize.Height);

            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            Graphics g = Graphics.FromImage(bitmap);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;

            if (bg != null)
                g.Clear((Color)bg);

            svgDoc.Draw(g);

            var memoryStream = new MemoryStream();
            ImageFormat imgFmt = (fmt == "jpg") ? ImageFormat.Jpeg : ImageFormat.Png;
            bitmap.Save(memoryStream, imgFmt);
            memoryStream.Position = 0;

            return memoryStream;
        }

        private static Stream ResizeImage(Image image, int newWidth, int newHeight, string fmt, double paddingProp = 0.3, Color? bg = null)
        {
            int adjustWidth;
            int adjustedHeight;
            int paddingW;
            int paddingH;
            if (paddingProp > 0)
            {
                paddingW = (int)(paddingProp * newWidth * 0.5);
                adjustWidth = newWidth - paddingW;
                paddingH = (int)(paddingProp * newHeight * 0.5);
                adjustedHeight = newHeight - paddingH;
            }
            else
            {
                paddingW = paddingH = 0;
                adjustWidth = newWidth;
                adjustedHeight = newHeight;
            }

            int width = image.Size.Width;
            int height = image.Size.Height;

            double ratioW = (double)adjustWidth / width;
            double ratioH = (double)adjustedHeight / height;

            double scaleFactor = ratioH > ratioW ? ratioW : ratioH;

            var scaledHeight = (int)(height * scaleFactor);
            var scaledWidth = (int)(width * scaleFactor);

            double originX = ratioH > ratioW ? paddingW * 0.5 : newWidth * 0.5 - scaledWidth * 0.5;
            double originY = ratioH > ratioW ? newHeight * 0.5 - scaledHeight * 0.5 : paddingH * 0.5;

            var srcBmp = new Bitmap(image);
            Color pixel = bg != null ? (Color)bg : srcBmp.GetPixel(0, 0);

            var bitmap = new Bitmap(newWidth, newHeight, srcBmp.PixelFormat);
            Graphics g = Graphics.FromImage(bitmap);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;

            g.Clear(pixel);

            var dstRect = new Rectangle((int)originX, (int)originY, scaledWidth, scaledHeight);

            using (var ia = new ImageAttributes())
            {
                ia.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(image, dstRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, ia);
            }

            var memoryStream = new MemoryStream();
            ImageFormat imgFmt = (fmt == "jpg") ? ImageFormat.Jpeg : ImageFormat.Png;
            bitmap.Save(memoryStream, imgFmt);
            memoryStream.Position = 0;

            return memoryStream;
        }
    }

    //    public class IconModel: IDisposable
    //    {
    //        private bool disposed = false;

    //        public string SvgFile { get; set; }

    //        public Image InputImage { get; set; }

    //        public double Padding { get; set; }

    //        public Color? Background { get; set; }

    //        public string[] Platforms { get; set; }

    //        public void Dispose()
    //        {
    //            Dispose(true);
    //            GC.SuppressFinalize(this);
    //        }

    //        protected virtual void Dispose(bool disposing)
    //        {
    //            if (disposed)
    //                return;

    //            if (disposing)
    //            {
    //                if (InputImage != null)
    //                {
    //                    InputImage.Dispose();
    //                }
    //            }

    //            disposed = true;
    //        }
    //    }

    //    public class ImageResponse
    //    {
    //        public Uri Uri { get; set; }
    //    }

    //    public class IconObject
    //    {
    //        public IconObject(string src, string size)
    //        {
    //            this.src = src;
    //            this.sizes = size;
    //        }

    //        public string src { get; set; }

    //        public string sizes { get; set; }
    //    }

    //    public class IconRootObject
    //    {
    //        public List<IconObject> icons { get; set; } = new List<IconObject>();
    //    }
}
