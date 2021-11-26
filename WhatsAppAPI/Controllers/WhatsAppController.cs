using Microsoft.AspNetCore.Mvc;
using PuppeteerSharp;
using System.Drawing;
using ZXing;

namespace WhatsAppAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WhatsAppController : ControllerBase
    {
        static Browser _browser;
        static Page _whatsAppPage;
        private IWebHostEnvironment _env;

        public WhatsAppController(IWebHostEnvironment env)
        {
            _env = env;
        }

        /// <summary>
        /// Abre o Chrome, entra na p�gina do WhatsApp, captura o QrCode e devolve para a nossa aplica��o
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            BrowserFetcher browserFetcher = new BrowserFetcher(new BrowserFetcherOptions()
            {
                Path = Path.Combine(_env.WebRootPath, "App_Data") //Abre o navegador que est� nessa pasta
            });

            //BrowserFetcher browserFetcher = new BrowserFetcher();
            //var teste = await browserFetcher.DownloadAsync(BrowserFetcher.DefaultRevision);
            //Essas linhas baixa o Chrome

            _browser = await Puppeteer.LaunchAsync(new LaunchOptions()
            {
                UserDataDir = Path.Combine(_env.WebRootPath, "myData"), //caminho onde salvar� o cache do Chrome.
                ExecutablePath = browserFetcher.GetExecutablePath(BrowserFetcher.DefaultRevision), //Caminho do execut�vel do Chrome
                Headless = false, //Vai rodar com a interface gr�fica ou n�o? False ele executa graficamente
                Args = new[] { "--no-sandbox" } //Argumentos extras. Para evitar incompatibilidade no servidor ele inseriu essa op��o
            });

            try
            {
                _whatsAppPage = await _browser.NewPageAsync();

                await _whatsAppPage.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/73.0.3641.0 Safari/537.36");
                await _whatsAppPage.GoToAsync("http://web.whatsapp.com/send?phone=8888888888&text=msg"); //Se tirar o n�mero e a msg n�o funciona

                await _whatsAppPage.WaitForSelectorAsync(WhatsAppMetadata.QrCode); //Se em 20 segundos n�o achar, vai dar timeout e dar erro na Aplica��o
                //Ele s� espera pela carga desse seletor. Pra garantir que abaixo quando pe�a para pega-lo ele j� esteja carregado

                var response = await _whatsAppPage.EvaluateExpressionAsync<string>(WhatsAppMetadata.QrScript);
                //Evaluate - Executa um scrip naquela p�gina do Chrome.

                var qrWriter = new BarcodeWriter();
                qrWriter.Format = BarcodeFormat.QR_CODE;
                qrWriter.Options = new ZXing.Common.EncodingOptions() { Height = 264, Width = 264, Margin = 0 };

                var bitmap = qrWriter.Write(response);
                var bitmapBytes = BitmapToBytes(bitmap);
                return File(bitmapBytes, "image/jpeg");
            }
            catch (Exception)
            {
                var response = _whatsAppPage.ScreenshotDataAsync().Result;
                return File(response, "image/jpeg");
            }
            finally
            {
                Response.OnCompleted(async () =>
                {
                    try
                    {
                        //Fechar o browser sempre pois n�o pode rodar 2 com o mesmo cache
                        await _whatsAppPage.WaitForSelectorAsync(WhatsAppMetadata.MainPanel);
                        await _browser.CloseAsync();
                    }
                    catch (Exception)
                    {
                        await _browser.CloseAsync();
                    }
                });
            }
        }

        [HttpGet("Message", Name = "Message")]
        public async Task<IActionResult> Message(String numero, String msg)
        {
            msg = msg.Replace(" ", "%20");
            BrowserFetcher browserFetcher = new BrowserFetcher(new BrowserFetcherOptions()
            {
                Path = Path.Combine(_env.WebRootPath, "App_Data") //Abre o navegador que est� nessa pasta
            });

            _browser = await Puppeteer.LaunchAsync(new LaunchOptions()
            {
                UserDataDir = Path.Combine(_env.WebRootPath, "myData"), //caminho onde salvar� o cache do Chrome.
                ExecutablePath = browserFetcher.GetExecutablePath(BrowserFetcher.DefaultRevision), //Caminho do execut�vel do Chrome
                Headless = false, //Vai rodar com a interface gr�fica ou n�o? False ele executa graficamente
                Args = new[] { "--no-sandbox" } //Argumentos extras. Para evitar incompatibilidade no servidor ele inseriu essa op��o
            });

            _whatsAppPage = await _browser.NewPageAsync();
            await _whatsAppPage.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/73.0.3641.0 Safari/537.36");
            var msgUrl = "http://web.whatsapp.com/send?phone=" + numero + "&text=" + msg;
            await _whatsAppPage.GoToAsync(WhatsAppMetadata.WhatsAppURL);
            //Mandamos para o endere�o padr�o do WhatsApp primeiro pois se abrir diretamente a msgUrl pode dar algum problema na hora de escrever a mensagem

            try
            {
                await _whatsAppPage.WaitForSelectorAsync(WhatsAppMetadata.MainPanel);
                await _whatsAppPage.GoToAsync(msgUrl);
                await _whatsAppPage.WaitForSelectorAsync(WhatsAppMetadata.MainPanel);
                var enter = await _whatsAppPage.QuerySelectorAsync("[data-testid=\"send\"]");
                
                int tryCount = 3;

                do
                {
                    enter = await _whatsAppPage.QuerySelectorAsync("[data-testid=\"send\"]");
                    tryCount--;
                }
                while (enter == null && tryCount > 0);

                if (enter is not null)
                    await enter.ClickAsync();

                //Copiar como � feito na Account o n�mero de tentativas e manter esse cara tentando ser pego

                return Ok();
            }
            catch (Exception)
            {
                var response1 = _whatsAppPage.ScreenshotDataAsync().Result;
                return File(response1, "image/jpeg");
            }
            finally
            {
                Response.OnCompleted(async () =>
                {
                    try
                    {
                        Thread.Sleep(5000);
                        //Fechar o browser sempre pois n�o pode rodar 2 com o mesmo cache
                        await _whatsAppPage.WaitForSelectorAsync(WhatsAppMetadata.MainPanel);
                        await _browser.CloseAsync();
                    }
                    catch (Exception)
                    {
                        await _browser.CloseAsync();
                    }
                });
            }
        }

        private static Byte[] BitmapToBytes(Bitmap img)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                img.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                return stream.ToArray();
            }
        }
    }
}