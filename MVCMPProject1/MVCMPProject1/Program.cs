using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using CsQuery;

namespace MVCMPProject1
{
    class Program
    {
        static void Main(string[] args)
        {
            Task t = MainAsync(args);
            t.Wait();          
        }

        private static async Task MainAsync(string[] args)
        {
            var options = new Options();
            if (Parser.Default.ParseArguments(args, options))
            {
                var url = options.Url;
                Uri inputUrl;
                try
                {
                    inputUrl = new Uri(url);
                }
                catch (Exception)
                {
                    Console.WriteLine(
                        "Entered url is invalid, please provide a valid absolute url");
                    return;
                }

                var outputFolder = options.OutputFolder;
                if (!Directory.Exists(outputFolder))
                {
                    Console.WriteLine(
                        "Entered output path does not exist or incorrect, please provide a valid path");
                }
                else 
                {
                    await GetContent(inputUrl, outputFolder, options.Recursive, options.Depth, options.Verbose, options.Domain);
                    Console.WriteLine("Done");
                }
            }
        }


        private static async Task GetContent(Uri inputUrl, string outputFolder, bool isRecursive, int depth, bool isVerbose,
            bool allowDifferentDomain)
        {
            if (isRecursive && depth == -1)
            {
                return;
            }
            CQ dom;
            using (var client = new HttpClient())
            {                
                HttpResponseMessage response = await client.GetAsync(inputUrl.OriginalString);
                if (isVerbose)
                    Console.WriteLine("Downloading " + inputUrl.OriginalString + "..." + "\n");
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var inputUrlToArray = inputUrl.OriginalString.Split('/');
                    var fileName = inputUrlToArray[inputUrlToArray.Length - 1];
                    var filepath = Path.Combine(outputFolder, fileName);

                        dom = response.Content.ReadAsStringAsync().Result;
                        dom = await GetResources(inputUrl, dom, "img", "src", isVerbose, allowDifferentDomain, outputFolder);
                        dom = await GetResources(inputUrl, dom, "link[href]", "href", isVerbose, allowDifferentDomain, outputFolder);
                        dom = await GetResources(inputUrl, dom, "script[src]", "src", isVerbose, allowDifferentDomain, outputFolder);

                    var fileContent = dom.Render();
                    //Debugger.Launch();
                    File.WriteAllText(filepath, fileContent);

                    foreach (var link in dom["a[href]"])
                        {
                            Uri url = new Uri(link.Attributes.GetAttribute("href"), UriKind.RelativeOrAbsolute);
                            if (!url.IsAbsoluteUri)
                            { 
                                url = new Uri(inputUrl.OriginalString + "/" + url.OriginalString);
                            }
                            if(IsSameDomain(inputUrl, url) || allowDifferentDomain)
                                 GetContent(url, outputFolder, isRecursive, depth - 1, isVerbose, allowDifferentDomain);
                        }
                    
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
                    Uri imgUrl = new Uri(element.Attributes.GetAttribute(src), UriKind.RelativeOrAbsolute);
                    //Debugger.Launch();
                    if (!IsAbsoluteUri(imgUrl))
                    {
                        imgUrl = new Uri(inputUrl.OriginalString + "/" + imgUrl.OriginalString);
                    }
                    if (IsSameDomain(imgUrl, inputUrl) || allowDifferentDomain)
                    {
                        if (isVerbose)
                            Console.WriteLine("Downloading " + imgUrl + "..." + "\n");
                        
                        imgUrl = new Uri(imgUrl.ToString().TrimStart('/'), UriKind.RelativeOrAbsolute);
                        if (!imgUrl.IsAbsoluteUri)
                            imgUrl = new Uri("http://" + imgUrl);
                        HttpResponseMessage imgResponse = await client.GetAsync(imgUrl.OriginalString);
                        if (imgResponse.StatusCode == HttpStatusCode.OK)
                        {
                            var imgFileName = Uri.UnescapeDataString(imgUrl.LocalPath)
                                .Trim('/')
                                .Replace("/", "\\");
                            var imgFilePath = Path.Combine(outputFolder, imgFileName);
                            var imgByteFile = imgResponse.Content.ReadAsByteArrayAsync().Result;
                            var numFolders = imgFilePath.Split('\\').Length;
                            var imgNameLength = imgFilePath.Split('\\')[numFolders - 1].Length;
                            var directory =
                                Directory.CreateDirectory(
                                    imgFilePath.Substring(0, imgFilePath.Length - imgNameLength)
                                        .TrimEnd('\\'));
                            using (FileStream fs = File.Create(imgFilePath))
                            {
                                foreach (byte t in imgByteFile)
                                {
                                    fs.WriteByte(t);
                                }
                            }
                            element.Attributes.SetAttribute("src", imgFileName.Replace('\\', '/'));
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
                domain1 = url1.GetLeftPart(UriPartial.Authority).Replace("/www.", "/").Replace("http://", "");
                domain2 = url2.GetLeftPart(UriPartial.Authority).Replace("/www.", "/").Replace("http://", "");
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

        private static bool IsFirstUriBase(Uri url1, Uri url2)
        {
            return url1.IsBaseOf(url2);
        }
    }

    

    class Options
    {
        [Option('u', "url", Required = true,
            HelpText = "Url link to download")]
        public string Url { get; set; }

        [Option('o', "output", Required = true,
            HelpText = "Output Folder")]
        public string OutputFolder { get; set; }

        [Option('r', "recursive", Required = false,
            HelpText = "Recursive download")]
        public bool Recursive { get; set; }

        [Option('l', "depth", Required = false, DefaultValue = 2,
            HelpText="Depth of recursive download, default value 2")]
        public int Depth { get; set; }

        [Option('v', "verbose", Required = false,
            HelpText = "Prints all messages to standard output.")]
        public bool Verbose { get; set; }

        [Option('k', "convertLink", Required = false,
            HelpText="Convert links for local viewing ")]
        public bool Convertlink { get; set; }

        [Option('p', "page", Required = false,
            HelpText="Download external images, css, and js")]
        public bool Page { get; set; }

        [Option('d', "domain", Required = false, DefaultValue = false,
            HelpText = "allow downloading of content from different domains, by default not allowed")]
        public bool Domain { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
              (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
