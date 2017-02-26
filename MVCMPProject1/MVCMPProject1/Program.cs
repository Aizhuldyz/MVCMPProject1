using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Resources;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommandLine;
using CsQuery;
using CsQuery.ExtensionMethods.Internal;
using Microsoft.SqlServer.Server;

namespace MVCMPProject1
{
    class Program
    {
        private static Uri _userInputUrl;
        private static ResourceManager _rm;
        static void Main(string[] args)
        {
            Task t = MainAsync(args);
            t.Wait();          
        }

        private static async Task MainAsync(string[] args)
        {
            _rm = ResourceManager.CreateFileBasedResourceManager("Resource.en.resx", "Resource.en.resx", null);
            var options = new Options();
            if (Parser.Default.ParseArguments(args, options))
            {
                var url = options.Url;
                Uri inputUrl;
                try
                {
                    inputUrl = new Uri(url);
                    _userInputUrl = inputUrl;
                }
                catch (Exception)
                {
                    Console.WriteLine(
                        _rm.GetString("InvalidUrlMessage"));
                    return;
                }

                var outputFolder = options.OutputFolder;
                if (!Directory.Exists(outputFolder))
                {
                    Console.WriteLine(
                        _rm.GetString("InvalidPathMessage"));
                }
                else 
                {
                    await GetContent(inputUrl, outputFolder, options.Recursive, options.Depth, options.Verbose, options.Domain);
                    Console.WriteLine(_rm.GetString("DoneDownloading"));
                }
            }
        }


        private static async Task GetContent(Uri inputUrl, string outputFolder, bool isRecursive, int depth, bool isVerbose,
            bool allowDifferentDomain, string recursivePageName = "")
        {
            if (isRecursive && depth == -1)
            {
                return;
            }
            using (var client = new HttpClient())
            {                
                HttpResponseMessage response = await client.GetAsync(inputUrl.OriginalString);
                if (isVerbose)
                    Console.Write(_rm.GetString("DownloadingMessage") + inputUrl.OriginalString + "..." + "\n");
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string filepath;
                    if (inputUrl.Equals(_userInputUrl))
                    {
                        var fileName = "index.html";
                        filepath = Path.Combine(outputFolder, fileName);
                    }
                    else
                    {
                        Debugger.Launch();
                        filepath = Path.Combine(outputFolder, recursivePageName);
                    }
                    CQ dom = response.Content.ReadAsStringAsync().Result;
                    dom = await GetResources(inputUrl, dom, "img", "src", isVerbose, allowDifferentDomain, outputFolder);
                    dom = await GetResources(inputUrl, dom, "link[href]", "href", isVerbose, allowDifferentDomain, outputFolder);
                    dom = await GetResources(inputUrl, dom, "script[src]", "src", isVerbose, allowDifferentDomain, outputFolder);

                    if (isRecursive)
                    {
                        foreach (var link in dom["a[href]"])
                        {
                            Debugger.Launch();
                            Uri url = new Uri(link.Attributes.GetAttribute("href"), UriKind.RelativeOrAbsolute);
                            if (!url.IsAbsoluteUri)
                            {
                                url = new Uri(inputUrl, url.OriginalString);
                            }
                            if (IsSameDomain(inputUrl, url) || allowDifferentDomain)
                            {
                                var 
                                    pageName = Path.GetFileName(url.AbsolutePath);
                                if (pageName.IsNullOrEmpty())
                                    continue;
                                Debugger.Launch();
                                var outputFolderNew = outputFolder +
                                                         inputUrl.AbsolutePath.Replace("/" + pageName, "");
                                pageName = pageName.Replace(".html", "").Replace(".htm", "");
                                pageName = pageName + ".html";

                                await GetContent(url, outputFolderNew, true, depth - 1, isVerbose,
                                        allowDifferentDomain, pageName);
                                link.Attributes.SetAttribute("href", url.OriginalString.Replace(url.Host, "/"));
                            }
                        }
                    }
                    var fileContent = dom.Render();
                    File.WriteAllText(filepath, string.Empty);
                    var isDir = (File.GetAttributes(filepath) & FileAttributes.Directory) == FileAttributes.Directory;
                    if (isDir)
                        filepath = Path.Combine(filepath, "index.html");
                    File.WriteAllText(filepath, fileContent);

                }
            }
            
        }

        private static async Task<CQ> GetResources(Uri inputUrl, CQ dom, string resource, string src, bool isVerbose,
            bool allowDifferentDomain, string outputFolder)
        {
            using (var client = new HttpClient())
            {
                foreach (var element in dom[resource])
                {
                    
                    var imgUrl = new Uri(element.Attributes.GetAttribute(src), UriKind.RelativeOrAbsolute);
                    if (!IsAbsoluteUri(imgUrl))
                    {
                        imgUrl = new Uri(inputUrl.OriginalString + "/" + imgUrl.OriginalString);
                    }
                    if (IsSameDomain(imgUrl, inputUrl) || allowDifferentDomain)
                    {
                        if (isVerbose)
                            Console.Write(_rm.GetString("DownloadingMessage") + imgUrl + "..." + "\n");
                        imgUrl = new Uri(imgUrl.ToString().TrimStart('/'), UriKind.RelativeOrAbsolute);
                        if (!imgUrl.IsAbsoluteUri)
                            imgUrl = new Uri("http://" + imgUrl);
                        var imgResponse = await client.GetAsync(imgUrl.OriginalString);
                        if (imgResponse.StatusCode == HttpStatusCode.OK)
                        {
                            var imgFileName = Uri.UnescapeDataString(imgUrl.LocalPath)
                                .Trim('/')
                                .Replace("/", "\\");
                            //if url contains # after host
                            if (imgFileName.IsNullOrEmpty())
                                continue;
                            var imgFilePath = Path.Combine(outputFolder, imgFileName);
                            var imgByteFile = await imgResponse.Content.ReadAsByteArrayAsync();
                            var numFolders = imgFilePath.Split('\\').Length;
                            var imgNameLength = imgFilePath.Split('\\')[numFolders - 1].Length;
                            var directory =
                                Directory.CreateDirectory(
                                    imgFilePath.Substring(0, imgFilePath.Length - imgNameLength)
                                        .TrimEnd('\\'));
                            using (var fs = File.Create(imgFilePath))
                            {
                                await fs.WriteAsync(imgByteFile, 0, imgByteFile.Length);
                            }
                            element.Attributes.SetAttribute(src, imgFileName.Replace('\\', '/'));
                        }
                    }
                }
            }
            return dom;
        }

        private static bool IsSameDomain(Uri url1, Uri url2)
        {
            string domain1;
            string domain2;
            try
            {
                domain1 = url1.GetLeftPart(UriPartial.Authority).Replace("/www.", "/").Replace("http://", "").Replace("https://", "");
                domain2 = url2.GetLeftPart(UriPartial.Authority).Replace("/www.", "/").Replace("http://", "").Replace("https://", "");
            }
            catch
            {
                return false;
            }
            return domain1.Equals(domain2);
        }

        private static bool IsAbsoluteUri(Uri url)
        {
            var r = new Regex("^(?:[a-z]+:)?//", RegexOptions.IgnoreCase);
            return r.IsMatch(url.OriginalString);
        }

    }
}
