using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ImageConverter
{
    class Program
    {
        static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
        }

        static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            new WebHostBuilder()
                .UseKestrel()
                .ConfigureServices(services =>
                {
                    services.AddMemoryCache();
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/{filename}", async context =>
                        {
                            /* http://localhost:5050/{filename} */
                            //http://localhost:5050/moja_slika.jpg
                            string filename = context.Request.RouteValues["filename"].ToString();
                            string path = Path.Combine(Directory.GetCurrentDirectory(), filename);

                            if (!File.Exists(path))
                            {
                                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                                return;
                            }

                            var cache = context.RequestServices.GetService<IMemoryCache>();
                            var cancellationTokenSource = new CancellationTokenSource();

                            byte[] gifBytes = cache.Get<byte[]>(filename);
                            if (gifBytes == null)
                            {
                                context.Response.ContentType = "image/gif";

                                using (var image = Image.Load<Rgba32>(path))
                                {
                                    // Pretvaranje slike u GIF sa raznim varijantama boja
                                    var frames = new List<Image<Rgba32>>();
                                    for (int i = 0; i < 10; i++)
                                    {
                                        image.Mutate(x => x.BlackWhite());
                                        frames.Add(image.Clone());
                                    }

                                    // Cuvanje GIF-a u stream-u
                                    using (var gifStream = new MemoryStream())
                                    {
                                        using (var gifEncoder = new SixLabors.ImageSharp.Formats.Gif.GifEncoder())
                                        {
                                            gifEncoder.Encode(gifStream, frames, 50);
                                            gifBytes = gifStream.ToArray();
                                            var cacheEntryOptions = new MemoryCacheEntryOptions()
                                                .SetSlidingExpiration(TimeSpan.FromSeconds(30));
                                            cache.Set(filename, gifBytes, cacheEntryOptions);
                                            gifStream.Seek(0, SeekOrigin.Begin);
                                            await gifStream.CopyToAsync(context.Response.Body, cancellationTokenSource.Token);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                context.Response.ContentType = "image/gif";
                                await context.Response.Body.WriteAsync(gifBytes, 0, gifBytes.Length, cancellationTokenSource.Token);
                            }
                        });
                    });
                });
    }
}