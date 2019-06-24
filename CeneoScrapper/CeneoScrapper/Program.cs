using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace CeneoScrapper
{
    class Program
    {
        static bool IsDigitsOnly(string str)
        {
            foreach (char c in str)
            {
                if (c < '0' || c > '9')
                    return false;
            }

            return true;
        }

        private static string _categoryName = "";
        private static List<string> _opinions = new List<string>();

        static void ParseProduct(string address,int reviewPage)
        {
            var productLink = "https://www.ceneo.pl/" + address + "/opinie-" + reviewPage;

            //create document
            var doc = new HtmlWeb();

            //
            productLink = productLink.Replace("###", "#");

            //load main link to it
            var document = doc.Load(productLink);

            //Console.WriteLine("Strona: " + productLink);

            try
            {
                //get li elements review-box js_product-review
                var reviewNodes = document.DocumentNode.SelectNodes("//li[@class='review-box js_product-review']").ToList();

                foreach (var reviewNode in reviewNodes)
                {
                    //get score
                    //<span class="review-score-count">4,5/5</span>
                    var productScore = reviewNode.SelectSingleNode(".//span[@class='review-score-count']").InnerText;

                    //get review
                    //<p class="product-review-body">
                    var productReview = reviewNode.SelectSingleNode(".//p[@class='product-review-body']").InnerText.Replace(System.Environment.NewLine, "");

                    //remove all new line characters
                    productReview = Regex.Replace(productReview, @"\t|\n|\r", "");

                    if (!string.IsNullOrEmpty(productScore) && !string.IsNullOrEmpty(productReview))
                    {
                        string opinion = productScore + " " + productReview;

                        if (!_opinions.Contains(opinion))
                        {
                            _opinions.Add(opinion);

                            File.AppendAllText("Opinie.txt", opinion + "\n");

                            commentCounter++;
                            Console.Write("\rIlosc opini: " + commentCounter);
                        }
                    }
                }

                //check if site contains next opinie
                if (document.DocumentNode.InnerHtml.Contains("opinie-" + (reviewPage + 1)))
                {
                    ParseProduct(address, reviewPage + 1);
                }
            }
            catch (Exception e)
            {
                return;
            }
        }

        static int commentCounter = 0;

        static void ParseCategory(string category, int page)
        {
            if (page == 0)
            {
                Console.WriteLine("\nKategoria: "+ category);
                commentCounter = 0;
            }

            //https://www.ceneo.pl/Smartfony;0020-30-0-0-4.htm


            var categoryLink = "https://www.ceneo.pl/" + category;

            if (page > 0)
            {
                categoryLink = categoryLink + ";0020-30-0-0-" + page + ".htm";
            }


            WebClient myWebClient = new WebClient();
            myWebClient.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)"); // If you need to simulate a specific browser
            byte[] myDataBuffer = myWebClient.DownloadData(categoryLink);
            string download = Encoding.ASCII.GetString(myDataBuffer);

            HtmlDocument document = new HtmlDocument();
            document.LoadHtml(download);

            _categoryName = category + ".txt";

            try
            {
                //get links only to poducts with reviews
                var productNodes = document.DocumentNode.SelectNodes("//a[@class='product-reviews-link dotted-link js_reviews-link js_clickHash js_seoUrl']").ToList();

                foreach (var productNode in productNodes)
                {
                    var lineczki = productNode.Attributes["href"].Value.Replace("/", "");

                    //Debug.WriteLine(lineczki);
                    ParseProduct(lineczki.Replace("###tab=reviews_scroll", ""), 1);
                }

                //check if there is next page
                var test = "/" + category + ";0020-30-0-0-" + (page + 1) + ".htm";
                if (document.DocumentNode.InnerHtml.Contains(test))
                {
                    //Console.WriteLine("Strona: " + test);
                    ParseCategory(category, page + 1);
                }
            }
            catch (Exception e)
            {
                return;
            }
        }

        static int GetNoOfWords(string s)
        {
            return s.Split(new char[] { ' ', '.', ',', '?' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        public static string RemoveDiacritics( string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            text = text.Normalize(NormalizationForm.FormD);
            var chars = text.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray();
            return new string(chars).Normalize(NormalizationForm.FormC);
        }

        static void PrepareDate()
        {
            string baseFile = "Opinie.txt";

            if (!File.Exists(baseFile))
            {
                return;
            }

            List<string> trainData = new List<string>();
            List<string> testData = new List<string>();

            //read all lines
            var lines = File.ReadAllLines(baseFile);
            int counter = 1;

            foreach (var line in lines)
            {
                //get score
                int pos = line.IndexOf(' ');

                string partScore = line.Substring(0, pos);
                string partLabel = line.Substring(pos+1, line.Length - pos-1);

                partLabel = RemoveDiacritics(partLabel).Replace(":", "").Replace("|", "");


                partScore = partScore.Replace(",", ".").Replace("/5", "");

                float score = float.Parse(partScore);
                int wordCount = GetNoOfWords(partLabel);


                //wszystkie litery male
                var labelSmall = partLabel.ToLower();

                //czy zawiera dobry;
                var propGoodWord = Regex.Matches(labelSmall, " dobry").Count; 

                //czt zawiera polecam
                var propPolecamWord = Regex.Matches(labelSmall, " polecam").Count;

                var propNiePolecamWord = Regex.Matches(labelSmall, "nie polecam").Count;

                var superCount = Regex.Matches(labelSmall, "super").Count;

                var okCount = Regex.Matches(labelSmall, " ok").Count;

                var low_camera = Regex.Matches(labelSmall, " słaby aparat").Count;



                //czy zawiera

                //3 '4 |f a |a word_count:1

                var trainLine = score + " '" + counter + " |f " + partLabel + " |a word_count:" + wordCount +
                                " good_count:" + propGoodWord + " polcam_count:" + propPolecamWord + " super_count:" +
                                superCount + " ok_count:" + okCount + " niepolecam: " + propNiePolecamWord +
                                " lowa_camera: " + low_camera;

                trainData.Add(trainLine);

                counter++;
            }

            int testAmmount = (int)(trainData.Count * 0.1);

            Random rnd = new Random();

            testData = trainData.OrderBy(x => rnd.Next()).Take(testAmmount).ToList();

            //remove test data from training data
            foreach (var testLine in testData)
            {
                trainData.Remove(testLine);
            }

            File.WriteAllLines( "Opinie_train.vw",trainData);
            File.WriteAllLines( "Opinie_test.vw", testData);
        }

        class PredictionTester
        {
            public float RealScore { get; set; }
            public float PredictedScore { get; set; }
        }

        static void PredTester(string testFile,string predFile)
        {
            Dictionary<int, PredictionTester> mapping = new Dictionary<int, PredictionTester>();

            var predLines = File.ReadAllLines(predFile);
            var testLines = File.ReadAllLines(testFile);

            //read all tests
            foreach (var testLine in testLines)
            {
                PredictionTester prediction = new PredictionTester();

                var testCut = testLine.Split(' ');

                int id = Convert.ToInt32(testCut[1].Replace("'",""));
                prediction.PredictedScore = Convert.ToSingle(testCut[0]);


                mapping.Add(id, prediction);
            }

            //read all predictions
            foreach (var predLine in predLines)
            {
                var testCut = predLine.Split(' ');

                int id = Convert.ToInt32(testCut[1]);

                mapping[id].RealScore = Convert.ToSingle(testCut[0]);
            }

            float score = 0.0f;

            //save all data
            foreach (var predictionTester in mapping)
            {
                File.AppendAllText("Wynik.txt", predictionTester.Key + " " + predictionTester.Value.RealScore + " " + predictionTester.Value.PredictedScore +  "\n");

                if (predictionTester.Value.RealScore > predictionTester.Value.PredictedScore)
                {
                    score += (predictionTester.Value.RealScore - predictionTester.Value.PredictedScore);
                }
                else
                {
                    score += (predictionTester.Value.PredictedScore - predictionTester.Value.RealScore);
                }
            }

            float realScore = score / mapping.Count;

            Console.WriteLine("Real Score: " + realScore);

        }

        static void ParseSourceFile(string fileName)
        {
            List<string> products = new List<string>();

            string test = File.ReadAllText(fileName);

            HtmlDocument document = new HtmlDocument();
            document.LoadHtml(test);

            //get links only to poducts with reviews
            var productNodes = document.DocumentNode.SelectNodes("//a[@class='js_seoUrl js_clickHash go-to-product']").ToList();

            foreach (var productNode in productNodes)
            {
                var lineczki = productNode.Attributes["href"].Value.Replace("/", "");
                lineczki = lineczki.Replace("###tab=reviews_scroll", "");

                if (lineczki.Length > 8)
                {
                    lineczki = lineczki.Substring(0, 8);
                }

                if (!products.Contains(lineczki))
                {
                    products.Add(lineczki);
                }
            }

            _categoryName = "Opinie.txt";

            foreach (var product in products)
            {
                ParseProduct(product, 1);
            }
        }

        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Brak parametrow.");

                //help
                Console.WriteLine("");
                Console.WriteLine("Uzycie programu:");
                Console.WriteLine("");

                Console.WriteLine("CeneoScrapper.exe -parse Uroda Smartforny [kolejny_dzial]  - Pobranie dancyh z wybranych dzialow");
                Console.WriteLine("CeneoScrapper.exe -file Plik.txt  - Pobranie dancyh z pliku");
                Console.WriteLine("CeneoScrapper.exe -prepare  Przygotowanie danych do uczenia");
                Console.WriteLine("CeneoScrapper.exe -test plik_testowt.vm preds.txt - Wyliczenie poziomu bledu");

                return;
            }

            if (args[0] == "-parse")
            {
                for (int i = 1; i < args.Length; i++)
                {
                    ParseCategory(args[i], 0);
                }
            }

            if (args[0] == "-file")
            {
                ParseSourceFile(args[1]);
            }

            if (args[0] == "-prepare")
            {
                PrepareDate();
            }

            if (args[0] == "-test")
            {
                PredTester(args[1], args[2]);
            }
        }
    }
}
