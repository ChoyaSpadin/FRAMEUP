// FRAMEUP - diagnóstico do PC. Só LÊ informações: nada é alterado aqui.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Frameup
{
    public enum Gravidade { Ok, Aviso, Problema }

    public class ItemDiagnostico
    {
        public string Titulo, Detalhe;
        public Gravidade Gravidade;
        public string Acao;        // o que o botão faz (ver Tela.AcaoDiagnostico)
        public string AcaoTexto;   // texto do botão; null = sem botão

        public ItemDiagnostico(string titulo, Gravidade g, string detalhe, string acao = null, string acaoTexto = null)
        {
            Titulo = titulo; Gravidade = g; Detalhe = detalhe; Acao = acao; AcaoTexto = acaoTexto;
        }
    }

    public static class Diagnostico
    {
        static readonly CultureInfo Br = new CultureInfo("pt-BR");

        public static List<ItemDiagnostico> Analisar()
        {
            var itens = new List<ItemDiagnostico>();
            var verificacoes = new Func<IEnumerable<ItemDiagnostico>>[] { Monitores, Memoria, DriverDeVideo, Disco, Energia, RecursosDeJogo, Inicializacao };
            foreach (var v in verificacoes)
            {
                try { itens.AddRange(v()); }
                catch { }   // uma verificação que falha não derruba as outras
            }
            return itens;
        }

        public static int Nota(List<ItemDiagnostico> itens)
        {
            int nota = 100 - 18 * itens.Count(i => i.Gravidade == Gravidade.Problema) - 7 * itens.Count(i => i.Gravidade == Gravidade.Aviso);
            return Math.Max(5, Math.Min(100, nota));
        }

        public static string Rotulo(int nota)
        {
            if (nota >= 90) return "Excelente";
            if (nota >= 75) return "Bom";
            if (nota >= 50) return "Precisa de atenção";
            return "Crítico";
        }

        // ------------------------------------------------------------ monitor

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct DispositivoTela
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct ModoTela
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool EnumDisplayDevicesW(string dispositivo, uint indice, ref DispositivoTela info, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool EnumDisplaySettingsW(string dispositivo, int modo, ref ModoTela info);

        static ModoTela NovoModo()
        {
            return new ModoTela { dmSize = (short)Marshal.SizeOf(typeof(ModoTela)) };
        }

        static IEnumerable<ItemDiagnostico> Monitores()
        {
            var itens = new List<ItemDiagnostico>();
            const int ligadoNaAreaDeTrabalho = 1, entrelacado = 2, atual = -1;
            var disp = new DispositivoTela { cb = Marshal.SizeOf(typeof(DispositivoTela)) };
            int numero = 0;
            for (uint i = 0; EnumDisplayDevicesW(null, i, ref disp, 0); i++)
            {
                if ((disp.StateFlags & ligadoNaAreaDeTrabalho) != 0)
                {
                    var agora = NovoModo();
                    if (EnumDisplaySettingsW(disp.DeviceName, atual, ref agora))
                    {
                        numero++;
                        int maximo = agora.dmDisplayFrequency;
                        var modo = NovoModo();
                        for (int k = 0; EnumDisplaySettingsW(disp.DeviceName, k, ref modo); k++)
                        {
                            if (modo.dmPelsWidth == agora.dmPelsWidth && modo.dmPelsHeight == agora.dmPelsHeight &&
                                (modo.dmDisplayFlags & entrelacado) == 0 && modo.dmDisplayFrequency > maximo)
                                maximo = modo.dmDisplayFrequency;
                            modo = NovoModo();
                        }
                        string nome = "Monitor " + numero;
                        string resolucao = agora.dmPelsWidth + "×" + agora.dmPelsHeight;
                        if (maximo - agora.dmDisplayFrequency >= 10)
                            itens.Add(new ItemDiagnostico(nome + ": " + agora.dmDisplayFrequency + " Hz",
                                maximo >= 100 ? Gravidade.Problema : Gravidade.Aviso,
                                resolucao + " a " + agora.dmDisplayFrequency + " Hz, mas ele aceita até " + maximo + " Hz. Você está jogando com menos quadros do que o monitor mostra.",
                                "monitor:" + maximo, "Corrigir"));
                        else
                            itens.Add(new ItemDiagnostico(nome + ": " + agora.dmDisplayFrequency + " Hz", Gravidade.Ok,
                                resolucao + " na taxa máxima que ele aceita."));
                    }
                }
                disp = new DispositivoTela { cb = Marshal.SizeOf(typeof(DispositivoTela)) };
            }
            return itens;
        }

        // ------------------------------------------------------------ memória

        static IEnumerable<ItemDiagnostico> Memoria()
        {
            uint nominal = 0, configurada = 0;
            using (var s = Otimizador.Buscar("SELECT Speed, ConfiguredClockSpeed FROM Win32_PhysicalMemory"))
                foreach (ManagementBaseObject o in s.Get())
                {
                    if (o["Speed"] != null) nominal = Math.Max(nominal, Convert.ToUInt32(o["Speed"]));
                    if (o["ConfiguredClockSpeed"] != null) configurada = Math.Max(configurada, Convert.ToUInt32(o["ConfiguredClockSpeed"]));
                }
            var total = Otimizador.Wmi("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem", "TotalPhysicalMemory");
            ulong bytes;
            double gb = total.Count > 0 && ulong.TryParse(total[0], out bytes) ? Math.Round(bytes / 1073741824.0) : 0;
            string tamanho = gb > 0 ? gb + " GB" : "Memória";
            string velocidade = configurada > 0 ? " a " + configurada + " MHz" : "";

            if (configurada > 0 && nominal > 0 && configurada < nominal * 0.93)
                return new[] { new ItemDiagnostico("Memória RAM abaixo da velocidade", Gravidade.Aviso,
                    tamanho + velocidade + ", mas os pentes suportam " + nominal + " MHz. Dá para ativar o XMP/DOCP na BIOS.",
                    "ram", "Como fazer") };
            if (gb > 0 && gb < 8)
                return new[] { new ItemDiagnostico("Pouca memória RAM", Gravidade.Aviso,
                    tamanho + velocidade + ". Jogos atuais pedem 8 GB ou mais; feche o navegador antes de jogar.") };
            return new[] { new ItemDiagnostico("Memória RAM", Gravidade.Ok, tamanho + velocidade + ".") };
        }

        // ------------------------------------------------------------ driver de vídeo

        static IEnumerable<ItemDiagnostico> DriverDeVideo()
        {
            var itens = new List<ItemDiagnostico>();
            using (var s = Otimizador.Buscar("SELECT Name, DriverDate FROM Win32_VideoController"))
                foreach (ManagementBaseObject o in s.Get())
                {
                    string nome = Convert.ToString(o["Name"]) ?? "";
                    if (Regex.IsMatch(nome, "Remote|Virtual|Mirror|Parsec|Meta", RegexOptions.IgnoreCase)) continue;
                    if (Regex.IsMatch(nome, "Basic Display|Básico", RegexOptions.IgnoreCase))
                    {
                        itens.Add(new ItemDiagnostico("Placa de vídeo sem driver", Gravidade.Problema,
                            "O Windows está usando o driver básico. Instale o driver da sua placa para os jogos rodarem direito."));
                        continue;
                    }
                    string data = Convert.ToString(o["DriverDate"]);
                    DateTime quando;
                    if (data == null || data.Length < 8 || !DateTime.TryParseExact(data.Substring(0, 8), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out quando))
                        continue;
                    int meses = (int)((DateTime.Now - quando).TotalDays / 30.4);
                    string site = Regex.IsMatch(nome, "NVIDIA|GeForce|RTX|GTX", RegexOptions.IgnoreCase) ? "https://www.nvidia.com/pt-br/drivers/"
                        : Regex.IsMatch(nome, "AMD|Radeon", RegexOptions.IgnoreCase) ? "https://www.amd.com/pt/support/download/drivers.html"
                        : "https://www.intel.com.br/content/www/br/pt/support/detect.html";
                    string quandoTexto = quando.ToString("MM/yyyy", Br);
                    if (meses >= 12)
                        itens.Add(new ItemDiagnostico("Driver de vídeo antigo", Gravidade.Aviso,
                            nome + ": driver de " + quandoTexto + " (" + (meses / 12 == 1 ? "1 ano" : meses / 12 + " anos") + "). Drivers novos corrigem bugs e melhoram jogos recentes.",
                            "url:" + site, "Baixar driver"));
                    else
                        itens.Add(new ItemDiagnostico("Driver de vídeo", Gravidade.Ok, nome + ": driver de " + quandoTexto + "."));
                }
            return itens;
        }

        // ------------------------------------------------------------ disco

        static IEnumerable<ItemDiagnostico> Disco()
        {
            var d = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
            double livreGb = d.AvailableFreeSpace / 1073741824.0;
            double totalGb = d.TotalSize / 1073741824.0;
            double pct = d.AvailableFreeSpace * 100.0 / d.TotalSize;
            string texto = string.Format(Br, "{0} {1:N0} GB livres de {2:N0} GB ({3:N0}%).", d.Name.TrimEnd('\\'), livreGb, totalGb, pct);
            if (pct < 10 || livreGb < 10)
                return new[] { new ItemDiagnostico("Disco do Windows quase cheio", Gravidade.Problema, texto + " Com pouco espaço o Windows e os jogos ficam lentos.", "limpeza", "Limpar") };
            if (pct < 20)
                return new[] { new ItemDiagnostico("Disco do Windows", Gravidade.Aviso, texto + " Vale liberar espaço.", "limpeza", "Limpar") };
            return new[] { new ItemDiagnostico("Disco do Windows", Gravidade.Ok, texto) };
        }

        // ------------------------------------------------------------ energia

        [StructLayout(LayoutKind.Sequential)]
        struct EstadoEnergia
        {
            public byte NaTomada, Bateria, PorcentagemBateria, EconomiaDeBateria;
            public int TempoRestante, TempoTotal;
        }

        [DllImport("kernel32.dll")]
        static extern bool GetSystemPowerStatus(out EstadoEnergia estado);

        static IEnumerable<ItemDiagnostico> Energia()
        {
            var itens = new List<ItemDiagnostico>();
            EstadoEnergia e;
            bool temBateria = GetSystemPowerStatus(out e) && e.Bateria != 128 && e.Bateria != 255;
            if (temBateria && e.NaTomada == 0)
                itens.Add(new ItemDiagnostico("Notebook na bateria", Gravidade.Aviso,
                    "Na bateria o Windows segura o desempenho. Para jogar, ligue na tomada."));

            string plano = Otimizador.PlanoAtivo();
            if (plano == null) return itens;
            bool? semEconomia = Otimizador.ProcessadorSemEconomia();
            bool desempenho = semEconomia ?? (plano == Otimizador.GuidPlanoFrameup || plano == Otimizador.GuidAltoDesempenho || plano == Otimizador.GuidDesempenhoMaximo);
            if (desempenho)
                itens.Add(new ItemDiagnostico("Plano de energia", Gravidade.Ok, "Plano de alto desempenho ativo."));
            else if (temBateria)
                itens.Add(new ItemDiagnostico("Plano de energia", Gravidade.Ok,
                    "Plano equilibrado (bom para notebook). Na tomada, use o modo \"Melhor desempenho\" do Windows."));
            else
                itens.Add(new ItemDiagnostico("Plano de energia econômico", Gravidade.Aviso,
                    "O processador pode economizar energia no meio do jogo.", "ajuste:plano", "Ativar desempenho"));
            return itens;
        }

        // ------------------------------------------------------------ jogos e inicialização

        static IEnumerable<ItemDiagnostico> RecursosDeJogo()
        {
            var itens = new List<ItemDiagnostico>();
            if (Otimizador.ModoDeJogoLigado())
                itens.Add(new ItemDiagnostico("Modo de Jogo", Gravidade.Ok, "Ligado."));
            else
                itens.Add(new ItemDiagnostico("Modo de Jogo desligado", Gravidade.Aviso,
                    "O Windows não está priorizando o jogo aberto.", "ajuste:modojogo", "Ligar"));
            if (Otimizador.GravacaoDesligada())
                itens.Add(new ItemDiagnostico("Gravação em segundo plano", Gravidade.Ok, "Desligada."));
            else
                itens.Add(new ItemDiagnostico("Gravação em segundo plano ligada", Gravidade.Aviso,
                    "O Xbox Game Bar pode gravar o jogo escondido e gastar desempenho.", "ajuste:gravacao", "Desligar"));
            return itens;
        }

        static IEnumerable<ItemDiagnostico> Inicializacao()
        {
            int n = Otimizador.ProgramasNaInicializacao().Count;
            if (n > 10)
                return new[] { new ItemDiagnostico(n + " programas na inicialização", Gravidade.Aviso,
                    "Muita coisa abrindo com o Windows pesa no boot e fica rodando durante o jogo.", "inicializacao", "Ver lista") };
            return new[] { new ItemDiagnostico("Programas na inicialização", Gravidade.Ok, n + " programas abrem com o Windows.", "inicializacao", "Ver lista") };
        }
    }
}
