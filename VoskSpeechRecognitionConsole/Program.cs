using NAudio.Wave;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using Vosk;

namespace VoskSpeechRecognitionConsole
{
    class Program
    {
        // Configurações
        private static string ModelPath = "model"; // Será convertido para caminho absoluto
        private const int SampleRate = 16000; // Taxa de amostragem para o reconhecimento
        private static string sourceLanguage = "pt"; // Idioma de origem padrão
        private static string targetLanguage = "en"; // Idioma de destino padrão


        static async Task Main(string[] args)
        {
            Console.WriteLine("==== TESTE DE RECONHECIMENTO DE FALA E TRADUÇÃO ====");
            Console.WriteLine("Escolha uma opção:");
            Console.WriteLine("1 - Testar apenas reconhecimento de fala (Vosk)");
            Console.WriteLine("2 - Testar apenas tradução de texto (LibreTranslate)");
            Console.WriteLine("3 - Testar reconhecimento de fala + tradução em tempo real");
            Console.Write("Sua escolha (1-3): ");

            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    await TestSpeechRecognition();
                    break;
                case "2":
                    await TranslationTest.RunTest();
                    break;
                case "3":
                    await TestSpeechRecognitionWithTranslation();
                    break;
                default:
                    Console.WriteLine("Opção inválida. Saindo...");
                    break;
            }

            Console.WriteLine("\nPressione qualquer tecla para sair...");
            Console.ReadKey();
        }


        static async Task TestSpeechRecognition()
        {
            
            // Obter e mostrar informações sobre os diretórios
            string currentDirectory = Directory.GetCurrentDirectory();
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string modelDirectoryRelative = Path.Combine(currentDirectory, ModelPath);
            string modelDirectoryAbsolute = Path.Combine(baseDirectory, ModelPath);

            Console.WriteLine("=== INFORMAÇÕES DE DIRETÓRIO ===");
            Console.WriteLine($"Diretório atual: {currentDirectory}");
            Console.WriteLine($"Diretório base: {baseDirectory}");
            Console.WriteLine($"Caminho relativo do modelo: {modelDirectoryRelative}");
            Console.WriteLine($"Caminho absoluto do modelo: {modelDirectoryAbsolute}");

            // Verificar os diversos caminhos possíveis
            if (Directory.Exists(modelDirectoryRelative))
            {
                Console.WriteLine("Modelo encontrado no caminho relativo!");
                ModelPath = modelDirectoryRelative;
            }
            else if (Directory.Exists(modelDirectoryAbsolute))
            {
                Console.WriteLine("Modelo encontrado no caminho absoluto!");
                ModelPath = modelDirectoryAbsolute;
            }
            else
            {
                // Tentar encontrar a pasta em outros lugares comuns
                string[] possiblePaths = {
                    Path.Combine(currentDirectory, "..\\model"),
                    Path.Combine(currentDirectory, "..\\..\\model"),
                    Path.Combine(currentDirectory, "..\\..\\..\\model"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\model")
                };

                foreach (string path in possiblePaths)
                {
                    string fullPath = Path.GetFullPath(path);
                    Console.WriteLine($"Verificando: {fullPath}");

                    if (Directory.Exists(fullPath))
                    {
                        Console.WriteLine($"Modelo encontrado em: {fullPath}");
                        ModelPath = fullPath;
                        break;
                    }
                }
            }

            Console.WriteLine("\nIniciando reconhecimento de fala com Vosk...");
            Console.WriteLine("Certifique-se de ter baixado o modelo e colocado na pasta 'model'");
            Console.WriteLine("Pressione qualquer tecla para iniciar a gravação...");
            Console.ReadKey();

            // Verifica novamente se o diretório do modelo existe
            if (!Directory.Exists(ModelPath))
            {
                Console.WriteLine($"ERROR: Diretório do modelo '{ModelPath}' não encontrado.");
                Console.WriteLine("Por favor, baixe um modelo do site https://alphacephei.com/vosk/models");
                Console.WriteLine("E descompacte-o para a pasta 'model'");
                Console.WriteLine("\nInforme o caminho completo para o diretório do modelo (ou pressione Enter para sair):");
                string customPath = Console.ReadLine();

                if (string.IsNullOrEmpty(customPath))
                    return;

                if (Directory.Exists(customPath))
                {
                    ModelPath = customPath;
                    Console.WriteLine($"Usando o caminho personalizado: {ModelPath}");
                }
                else
                {
                    Console.WriteLine("Caminho inválido. Saindo...");
                    return;
                }
            }

            Model model = null;
            VoskRecognizer recognizer = null;
            WaveInEvent waveIn = null;
            bool modelLoadFailed = false;

            try
            {
                // Inicializa o modelo Vosk
                Console.WriteLine("Carregando o modelo...");
                model = new Model(ModelPath);
                Console.WriteLine("Modelo carregado com sucesso!");

                // Configura o reconhecedor
                recognizer = new VoskRecognizer(model, SampleRate);
                recognizer.SetMaxAlternatives(0);
                recognizer.SetWords(true);

                // Configura a entrada de áudio
                Console.WriteLine("Iniciando captura de áudio do microfone...");
                waveIn = new WaveInEvent
                {
                    DeviceNumber = 0, // Usa o microfone padrão
                    WaveFormat = new WaveFormat(SampleRate, 1) // Mono, 16kHz
                };

                var stopRecording = new ManualResetEvent(false);

                // Variável para armazenar o resultado final
                string finalResultText = string.Empty;

                waveIn.DataAvailable += (sender, e) =>
                {
                    try
                    {
                        // Converte PCM para 16-bit
                        if (recognizer.AcceptWaveform(e.Buffer, e.BytesRecorded))
                        {
                            var result = recognizer.Result();
                            finalResultText = result; // Armazena o último resultado completo
                            ProcessResult(result);
                        }
                        else
                        {
                            var partialResult = recognizer.PartialResult();
                            ProcessPartialResult(partialResult);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro durante reconhecimento: {ex.Message}");
                    }
                };

                waveIn.RecordingStopped += (sender, e) =>
                {
                    stopRecording.Set();
                };

                // Inicia a gravação
                waveIn.StartRecording();
                Console.WriteLine("Gravação iniciada! Fale alguma coisa...");
                Console.WriteLine("Pressione Enter para parar a gravação");

                Console.ReadLine();

                // Para a gravação
                waveIn.StopRecording();
                Console.WriteLine("Processando resultados finais...");

                // Evitamos chamar FinalResult() diretamente para evitar o erro
                // Em vez disso, usamos o último resultado armazenado
                if (!string.IsNullOrEmpty(finalResultText))
                {
                    Console.WriteLine("Usando o último resultado capturado:");
                    ProcessResult(finalResultText);
                }
                else
                {
                    Console.WriteLine("Nenhum resultado final disponível.");
                }

                Console.WriteLine("Reconhecimento concluído!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                modelLoadFailed = true;
            }
            finally
            {
                // Liberação explícita de recursos
                if (waveIn != null)
                {
                    try { waveIn.StopRecording(); } catch { }
                    waveIn.Dispose();
                }

                if (recognizer != null && !modelLoadFailed)
                {
                    try { recognizer.Dispose(); } catch { }
                }

                if (model != null && !modelLoadFailed)
                {
                    try { model.Dispose(); } catch { }
                }

                // Força a coleta de lixo para liberar recursos
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            Console.WriteLine("Pressione qualquer tecla para sair...");
            Console.ReadKey();
        }

        static void ProcessResult(string resultJson)
        {
            try
            {
                var result = JsonSerializer.Deserialize<VoskResult>(resultJson);
                if (!string.IsNullOrEmpty(result?.Text))
                {
                    Console.WriteLine($"Reconhecido: {result.Text}");

                    // Aqui você pode adicionar código para traduzir o texto
                    Console.WriteLine($"Tradução simulada: [{result.Text}]");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar resultado: {ex.Message}");
            }
        }

        static void ProcessPartialResult(string partialResultJson)
        {
            try
            {
                var result = JsonSerializer.Deserialize<VoskPartialResult>(partialResultJson);
                if (!string.IsNullOrEmpty(result?.Partial))
                {
                    Console.Write($"\rParcial: {result.Partial}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar resultado parcial: {ex.Message}");
            }
        }

        static async Task TestSpeechRecognitionWithTranslation()
        {
            Console.WriteLine("\n== TESTE DE RECONHECIMENTO DE FALA COM TRADUÇÃO ==");

            // Configurar idiomas
            Console.Write("Idioma de origem (pt, en, es, fr, de, etc.): ");
            string input = Console.ReadLine()?.ToLower() ?? "";
            if (!string.IsNullOrEmpty(input)) sourceLanguage = input;

            Console.Write("Idioma de destino (pt, en, es, fr, de, etc.): ");
            input = Console.ReadLine()?.ToLower() ?? "";
            if (!string.IsNullOrEmpty(input)) targetLanguage = input;

            Console.WriteLine("Certifique-se de ter baixado o modelo adequado para o idioma de origem");
            Console.WriteLine("Pressione qualquer tecla para iniciar a gravação...");
            Console.ReadKey();

            // Verifica se o diretório do modelo existe
            if (!Directory.Exists(ModelPath))
            {
                Console.WriteLine($"ERROR: Diretório do modelo '{ModelPath}' não encontrado.");
                Console.WriteLine("Por favor, baixe um modelo do site https://alphacephei.com/vosk/models");
                Console.WriteLine("E descompacte-o para a pasta 'model'");
                return;
            }

            try
            {
                // Inicializa o modelo Vosk
                Console.WriteLine("Carregando o modelo...");
                using var model = new Model(ModelPath);
                Console.WriteLine("Modelo carregado com sucesso!");

                // Configura o reconhecedor
                using var recognizer = new VoskRecognizer(model, SampleRate);
                recognizer.SetMaxAlternatives(0);
                recognizer.SetWords(true);

                // Configura a entrada de áudio
                Console.WriteLine("Iniciando captura de áudio do microfone...");
                using var waveIn = new WaveInEvent
                {
                    DeviceNumber = 0, // Usa o microfone padrão
                    WaveFormat = new WaveFormat(SampleRate, 1) // Mono, 16kHz
                };

                var stopRecording = new ManualResetEvent(false);

                waveIn.DataAvailable += async (sender, e) =>
                {
                    // Converte PCM para 16-bit
                    if (recognizer.AcceptWaveform(e.Buffer, e.BytesRecorded))
                    {
                        var result = recognizer.Result();
                        await ProcessResultWithTranslation(result);
                    }
                    else
                    {
                        var partialResult = recognizer.PartialResult();
                        ProcessPartialResult(partialResult);
                    }
                };

                waveIn.RecordingStopped += (sender, e) =>
                {
                    stopRecording.Set();
                };

                // Inicia a gravação
                waveIn.StartRecording();
                Console.WriteLine("Gravação iniciada! Fale alguma coisa...");
                Console.WriteLine("Pressione Enter para parar a gravação");

                Console.ReadLine();

                // Para a gravação
                waveIn.StopRecording();
                Console.WriteLine("Processando resultados finais...");

                // Processa o último resultado (se houver)
                var finalResult = recognizer.FinalResult();
                await ProcessResultWithTranslation(finalResult);

                Console.WriteLine("Reconhecimento e tradução concluídos!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        

        static async Task ProcessResultWithTranslation(string resultJson)
        {
            try
            {
                var result = JsonSerializer.Deserialize<VoskResult>(resultJson);
                if (!string.IsNullOrEmpty(result?.Text))
                {
                    Console.WriteLine($"\nReconhecido [{sourceLanguage}]: {result.Text}");

                    // Traduzir o texto
                    try
                    {
                        var translatedText = await TranslationTest.TranslateText(result.Text, sourceLanguage, targetLanguage);
                        Console.WriteLine($"Traduzido [{targetLanguage}]: {translatedText}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erro na tradução: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar resultado: {ex.Message}");
            }
        }

       
    
    }

    class VoskResult
    {
        public string Text { get; set; } = string.Empty;
    }

    class VoskPartialResult
    {
        public string Partial { get; set; } = string.Empty;
    }
}