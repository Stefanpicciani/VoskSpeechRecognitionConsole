using NAudio.Wave;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using Vosk;

namespace VoskSpeechRecognitionConsoles
{
    class Program_
    {
        // Configurações
        private static string ModelPath = "model";
        private const int SampleRate = 16000;

        static void Main_(string[] args)
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
            bool modelFound = false;
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
                        modelFound = true;
                        break;
                    }
                }
            }

            if (!modelFound)
            {
                Console.WriteLine("Modelo não encontrado nos caminhos padrão.");
                Console.Write("Informe o caminho completo para o diretório do modelo: ");
                string userPath = Console.ReadLine();

                if (string.IsNullOrEmpty(userPath) || !Directory.Exists(userPath))
                {
                    Console.WriteLine("Caminho inválido ou não informado. Encerrando programa.");
                    return;
                }

                ModelPath = userPath;
                Console.WriteLine($"Usando caminho fornecido: {ModelPath}");
            }

            // Testar se o modelo pode ser carregado
            try
            {
                Console.WriteLine("Tentando carregar o modelo Vosk...");
                using (var model = new Model(ModelPath))
                {
                    Console.WriteLine("Modelo carregado com sucesso!");

                    Console.WriteLine("\nPressione qualquer tecla para iniciar a gravação de áudio...");
                    Console.ReadKey(true);

                    StartAudioCapture(model);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao carregar o modelo: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine("\nPrograma finalizado. Pressione qualquer tecla para sair...");
            Console.ReadKey();
        }

        static void StartAudioCapture(Model model)
        {
            try
            {
                // Criar reconhecedor
                using (var recognizer = new VoskRecognizer(model, SampleRate))
                {
                    recognizer.SetMaxAlternatives(0);

                    // Configurar captura de áudio
                    using (var waveIn = new WaveInEvent())
                    {
                        waveIn.WaveFormat = new WaveFormat(SampleRate, 1);

                        // Usar um buffer local para evitar chamadas diretas ao Result()
                        var buffer = new byte[4096];
                        var isFirstResult = true;

                        // Configurar evento de recepção de dados
                        waveIn.DataAvailable += (s, e) =>
                        {
                            try
                            {
                                // Verificar se temos dados o suficiente
                                if (e.BytesRecorded > 0)
                                {
                                    if (recognizer.AcceptWaveform(e.Buffer, e.BytesRecorded))
                                    {
                                        try
                                        {
                                            string json = recognizer.Result();
                                            if (!string.IsNullOrEmpty(json))
                                            {
                                                var result = JsonSerializer.Deserialize<VoskResult>(json);
                                                if (!string.IsNullOrEmpty(result?.Text))
                                                {
                                                    if (isFirstResult)
                                                    {
                                                        Console.WriteLine("\nTexto reconhecido:");
                                                        isFirstResult = false;
                                                    }

                                                    Console.WriteLine($" > {result.Text}");
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"Erro ao processar resultado: {ex.Message}");
                                        }
                                    }
                                    else
                                    {
                                        // Pegar resultado parcial, mas não usamos para evitar poluir o console
                                        string partialJson = recognizer.PartialResult();

                                        // Exibir apenas se for uma frase parcial significativa
                                        try
                                        {
                                            var partialResult = JsonSerializer.Deserialize<VoskPartialResult>(partialJson);
                                            if (!string.IsNullOrEmpty(partialResult?.Partial) && partialResult.Partial.Length > 5)
                                            {
                                                Console.Write($"\rParcial: {partialResult.Partial}");
                                            }
                                        }
                                        catch { }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Erro durante a captura: {ex.Message}");
                            }
                        };

                        // Iniciar gravação
                        Console.WriteLine("Iniciando captura de áudio. Fale algo...");
                        Console.WriteLine("Pressione Enter para parar.");
                        waveIn.StartRecording();

                        Console.ReadLine();

                        // Parar gravação
                        waveIn.StopRecording();
                        Console.WriteLine("\nGravação finalizada.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro durante a captura de áudio: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }

    // Classes para deserialização dos resultados
    class VoskResult
    {
        public string Text { get; set; } = string.Empty;
    }

    class VoskPartialResult
    {
        public string Partial { get; set; } = string.Empty;
    }
}