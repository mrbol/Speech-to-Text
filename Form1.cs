using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Google.Cloud.Speech.V1;
using NAudio;
using NAudio.Wave;
using System.Diagnostics;
using System.Configuration;
using System.IO;

namespace Speech_to_Text
{
    public partial class FrmPrincipal : Form
    {
        public int Total { get; set; }

        public FrmPrincipal()
        {
            InitializeComponent();
        }

        private void inicializar()
        {
            string origem = Convert.ToString(ConfigurationManager.AppSettings["pasta"]);

            this.Text = string.Concat("Processando ", Convert.ToString(ConfigurationManager.AppSettings["pasta"]));

            if (Directory.Exists(origem))
            {
                RegistraLog("Iniciou", string.Concat("entrou em ", DateTime.Now.ToString()));
                this.fileSystemWatcher1.Path = origem;
                this.fileSystemWatcher1.NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.FileName;
                fileSystemWatcher1.IncludeSubdirectories = false;
                fileSystemWatcher1.EnableRaisingEvents = true;

            }
            else
            {
                RegistraLog("Erro", string.Concat("Diretorio não existe ", DateTime.Now.ToString()));
            }

        }
        private void fileSystemWatcher1_Created(object sender, System.IO.FileSystemEventArgs e)
        {
            Total = Total + 1;
            lblQuantidade.Text = string.Concat("Lidos : ", Convert.ToString(Total));
            lblQuantidade.Refresh();

            StringBuilder builder = new StringBuilder();

            builder.AppendFormat("Cria\x00e7\x00e3o: {0} {1}", e.FullPath, Environment.NewLine);
            builder.AppendFormat("Nome: {0} {1}", e.Name, Environment.NewLine);
            builder.AppendFormat("Evento: {0} {1}", e.ChangeType, Environment.NewLine);
            builder.AppendFormat("----------------------- {0}", Environment.NewLine);
            RegistraLog("Novo Arquivo Adicionado ao Diretorio", builder.ToString());

            //recupera as informações das pastas a serem utilizadas
            string origem = Convert.ToString(ConfigurationManager.AppSettings["pasta"]);
            string temp = Convert.ToString(Path.Combine(Convert.ToString(ConfigurationManager.AppSettings["destino"]), "temp"));
            string processado = Convert.ToString(ConfigurationManager.AppSettings["processado"]);
            string erro = Convert.ToString(ConfigurationManager.AppSettings["erro"]);

            //prepara as pastas
            if (!Directory.Exists(Path.Combine(origem, "temp")))
            {
                Directory.CreateDirectory(Path.Combine(origem, "temp"));
            }
            if (!Directory.Exists(processado))
            {
                Directory.CreateDirectory(processado);
            }
            if (!Directory.Exists(erro))
            {
                Directory.CreateDirectory(erro);
            }

            if (!File.Exists(Path.Combine(processado, e.Name)))
            {

                //codifica o audio no formato esperado pela google

                if (ExecutarCMD(string.Format(@"c:\sox\sox {0} -r 8000 -b 16 {1}", Path.Combine(origem, e.Name), Path.Combine(origem, "temp", e.Name))))
                {
                    //transcreve o audio
                    string textoGoogle = RetornaTexto(File.ReadAllBytes(Path.Combine(origem, "temp", e.Name)), e.FullPath, Path.Combine(erro, e.Name));

                    RegistraLog("NOVO", "RetornaTexto realizado");

                }
                else
                {
                    RegistraLog("ERRO", "Falha ao compactar arquivo");

                    if (!File.Exists(Path.Combine(erro, e.Name)))
                    {
                        File.Move(e.FullPath, Path.Combine(erro, e.Name));
                    }
                    else
                    {
                        File.Delete(Path.Combine(erro, e.Name));
                        File.Move(e.FullPath, Path.Combine(erro, e.Name));
                    }

                    RegistraLog("ERRO", "Arquivo movido para pasta ERRO");

                }
            }


        }

        public bool ExecutarCMD(string comando)
        {
            bool retorno = false;
            using (Process processo = new Process())
            {
                processo.StartInfo.FileName = Environment.GetEnvironmentVariable("comspec");

                // Formata a string para passar como argumento para o cmd.exe
                processo.StartInfo.Arguments = string.Format("/c {0}", comando);

                processo.StartInfo.RedirectStandardOutput = true;
                processo.StartInfo.UseShellExecute = false;
                processo.StartInfo.CreateNoWindow = true;

                processo.Start();
                processo.WaitForExit();

                if (processo.ExitCode == 0)
                {
                    retorno = true;
                }

            }

            return retorno;
        }


        private string RetornaTexto(byte[] conteudo, string origem, string destino)
        {
            StringBuilder retorno = new StringBuilder();

            try
            {
                var speech = SpeechClient.Create();
                var response = speech.Recognize(new RecognitionConfig()
                {
                    //Encoding = RecognitionConfig.Types.AudioEncoding.Linear16,
                    //SampleRateHertz = 8000,
                    LanguageCode = "pt-BR",

                }, RecognitionAudio.FromBytes(conteudo));

                foreach (var result in response.Results)
                {
                    foreach (var alternative in result.Alternatives)
                    {
                        retorno.AppendLine(alternative.Transcript);
                    }
                }

                if (string.IsNullOrEmpty(retorno.ToString()))
                {
                    if (!File.Exists(destino))
                    {
                        File.Move(origem, destino);
                    }
                    else
                    {
                        File.Delete(destino);
                        File.Move(origem, destino);
                    }

                    RegistraLog("AVISO", "Retorno Audio vazio. Movido para pasta ERRO");
                }
            }
            catch (Exception ex)
            {

                if (!File.Exists(destino))
                {
                    File.Move(origem, destino);
                }
                else
                {
                    File.Delete(destino);
                    File.Move(origem, destino);
                }

                RegistraLog("ERRO", string.Concat(ex.Message, " ", (ex.InnerException != null ? ex.InnerException.Message : string.Empty)));
                RegistraLog("ERRO", "Arquivo movido para pasta ERRO");
            }
            finally
            {

            }

            return retorno.ToString();

        }

        private void RegistraLog(string tipo, string conteudo)
        {
            //string path = Convert.ToString(ConfigurationManager.AppSettings["caminhoLog"]);
            //if (!Directory.Exists(Path.GetDirectoryName(path)))
            //{
            //    Directory.CreateDirectory(Path.GetDirectoryName(path));
            //}

            //StreamWriter writer = new StreamWriter(path, true);
            //writer.WriteLine(string.Concat("Monitor : ", tipo, " - ", conteudo, " - ", DateTime.Now.ToString()));
            //writer.Flush();
            //writer.Close();

            txtConteudo.AppendText(string.Concat(" Monitor : ", tipo, " - ", conteudo, " - ", DateTime.Now.ToString(), Environment.NewLine));
            txtConteudo.Refresh();
        }

        private void RegistraTexto(string nome, string conteudo)
        {

            string path = string.Concat(Convert.ToString(ConfigurationManager.AppSettings["caminhoLog"]), nome, ".txt");

            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }

            using (StreamWriter writer = new StreamWriter(path, true))
            {

                writer.WriteLine(conteudo);
                writer.Flush();
                writer.Close();

            }

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Total = 0;
            inicializar();

        }
    }
}
