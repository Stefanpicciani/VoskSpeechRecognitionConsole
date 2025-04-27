using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace VoskSpeechRecognitionConsole
{
    // Esta classe pode ser usada para testar a tradução de texto com LibreTranslate
    // Você pode executá-la separadamente ou integrá-la ao programa principal
    public class TranslationTest
    {
        private const string LibreTranslateUrl = "http://localhost:5000/translate"; // Servidor local do LibreTranslate
        //private const string LibreTranslateUrl = "https://libretranslate.de/translate"; // Servidor público do LibreTranslate

        public static async Task RunTest()
        {
            Console.WriteLine("=== TESTE DE TRADUÇÃO ===");
            Console.Write("Digite o texto a ser traduzido: ");
            string text = Console.ReadLine() ?? "Olá, mundo!";

            Console.WriteLine("Idioma de origem (pt-BR, en, es, fr, de, etc.): ");
            string sourceLanguage = Console.ReadLine()?.ToLower() ?? "pt-BR";

            Console.WriteLine("Idioma de destino (pt-BR, en, es, fr, de, etc.): ");
            string targetLanguage = Console.ReadLine()?.ToLower() ?? "en";

            try
            {
                var translatedText = await TranslateText(text, sourceLanguage, targetLanguage);

                Console.WriteLine("\nResultado da tradução:");
                Console.WriteLine($"Texto original [{sourceLanguage}]: {text}");
                Console.WriteLine($"Texto traduzido [{targetLanguage}]: {translatedText}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nErro durante a tradução: {ex.Message}");

                // Informações adicionais para diagnóstico
                if (ex is HttpRequestException httpEx)
                {
                    Console.WriteLine($"Status code: {httpEx.StatusCode}");
                    Console.WriteLine("Verifique se o Docker do LibreTranslate está rodando em: http://localhost:5000/translate");
                    Console.WriteLine("Execute: docker run -d -p 5000:5000 libretranslate/libretranslate");
                }
            }
        }

        public static async Task<string> TranslateText(string text, string sourceLanguage, string targetLanguage)
        {
            using var httpClient = new HttpClient();

            // Verificar se o serviço está acessível
            try
            {
                var healthCheck = await httpClient.GetAsync("http://localhost:5000/languages");
                if (!healthCheck.IsSuccessStatusCode)
                {
                    throw new Exception("LibreTranslate não está acessível. Verifique se o Docker está rodando.");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao conectar ao LibreTranslate: {ex.Message}");
            }


            // Criar a requisição para o LibreTranslate
            var requestData = new
            {
                q = text,
                source = sourceLanguage,
                target = targetLanguage,
                format = "text",
                //api_key = "" // Deixe vazio para servidores públicos ou coloque sua chave se tiver uma
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestData),
                Encoding.UTF8,
                "application/json");

            var response = await httpClient.PostAsync(LibreTranslateUrl, content);
            response.EnsureSuccessStatusCode();

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Erro na API: {response.StatusCode}. Detalhes: {errorContent}");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var translationResult = JsonSerializer.Deserialize<TranslationResponse>(jsonResponse);

            return translationResult?.translatedText ?? string.Empty;
        }

        private class TranslationResponse
        {
            public string translatedText { get; set; } = string.Empty;
        }
    }
}