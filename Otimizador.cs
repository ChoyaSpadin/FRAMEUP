// FRAMEUP - motor de otimização.
//
// Regra de ouro: antes de mudar qualquer configuração, o valor original é
// gravado no backup (em disco). Cada ajuste guarda seus valores num "grupo",
// então dá para desfazer um ajuste sozinho ou tudo de uma vez. Nada aqui mexe
// em antivírus, Windows Update, drivers ou serviços.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace Frameup
{
    public enum Status { Ok, Aviso, Erro }

    public class Resultado
    {
        public Status Status;
        public string Detalhe;
        public string Codigo;

        public Resultado(Status status, string detalhe, string codigo)
        {
            Status = status; Detalhe = detalhe ?? ""; Codigo = codigo ?? "";
        }

        public static Resultado Ok(string d) { return new Resultado(Status.Ok, d, ""); }
        public static Resultado Aviso(string d) { return new Resultado(Status.Aviso, d, ""); }
        public static Resultado Erro(string d) { return new Resultado(Status.Erro, d, ""); }
    }

    public class ItemBackup
    {
        public string Id { get; set; }
        public string Grupo { get; set; }     // ajuste que gravou este valor
        public string Raiz { get; set; }      // HKCU ou HKLM
        public string Caminho { get; set; }   // sem a raiz
        public string Nome { get; set; }
        public bool Existia { get; set; }
        public string Tipo { get; set; }      // RegistryValueKind
        public string Valor { get; set; }     // valor original codificado como texto
    }

    public class Backup
    {
        public List<ItemBackup> Registro { get; set; }
        public List<string> ChavesCriadas { get; set; }   // "HKCU\caminho"
        public string PlanoOriginal { get; set; }
        public string UltimaOtimizacao { get; set; }

        public Backup()
        {
            Registro = new List<ItemBackup>();
            ChavesCriadas = new List<string>();
        }

        public Backup Copia()
        {
            var c = new Backup();
            c.Registro.AddRange(Registro);
            c.ChavesCriadas.AddRange(ChavesCriadas);
            c.PlanoOriginal = PlanoOriginal;
            c.UltimaOtimizacao = UltimaOtimizacao;
            return c;
        }

        public bool TemAlteracoes()
        {
            return Registro.Count > 0 || ChavesCriadas.Count > 0 || !string.IsNullOrEmpty(PlanoOriginal);
        }
    }

    public class InfoPC
    {
        public string Cpu = "—", Gpu = "—", Ram = "—", Windows = "—";
    }

    public class Otimizador
    {
        public const string Versao = "1.1";
        internal const string GuidPlanoFrameup = "7f3c1e2a-5b8d-4c6e-9a1f-2d4b6c8e0f13";
        internal const string GuidAltoDesempenho = "8c5e7fda-e8bf-4a96-9a85-cf2a7bb5ef5d";
        internal const string GuidDesempenhoMaximo = "e9a42b02-d5df-448d-aa00-03f14749eb61";
        const string GuidEquilibrado = "381b4222-f694-41f0-9685-ff5bb260df2e";
        const string SubgrupoUsb = "2a737441-1930-4402-8d77-b2bebba308a3";
        const string SuspensaoUsb = "48e6b7a6-50f5-4782-a5d4-53bb8f07e226";

        const string Perfil = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
        const string PerfilJogos = Perfil + @"\Tasks\Games";
        const string Conteudo = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
        static readonly string[] Sugestoes =
        {
            "SubscribedContent-338388Enabled", "SubscribedContent-338389Enabled", "SubscribedContent-310093Enabled",
            "SubscribedContent-353698Enabled", "SystemPaneSuggestionsEnabled", "SoftLandingEnabled", "SilentInstalledAppsEnabled"
        };

        public readonly string PastaDados;
        public readonly string ArquivoBackup;
        public readonly string ArquivoLog;
        public Backup Backup { get; private set; }
        public bool BackupInvalido { get; private set; }

        readonly object trava = new object();
        readonly string pastaDoPrograma;

        public Otimizador()
            : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FRAMEUP"))
        {
        }

        public Otimizador(string pastaDados)
        {
            PastaDados = pastaDados;
            // Um backup por usuário do Windows: os ajustes em HKCU são de cada conta.
            string sid = WindowsIdentity.GetCurrent().User.Value;
            ArquivoBackup = Path.Combine(PastaDados, "backup-" + sid + ".json");
            ArquivoLog = Path.Combine(PastaDados, "log.txt");
            pastaDoPrograma = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            Backup = LerBackup();
        }

        // ------------------------------------------------------------ log e backup

        public void Log(string texto)
        {
            try
            {
                Directory.CreateDirectory(PastaDados);
                File.AppendAllText(ArquivoLog,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + texto + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }

        // Um backup ilegível NÃO vira backup vazio (isso perderia os valores
        // originais): o FRAMEUP bloqueia as alterações até o arquivo ser resolvido.
        Backup LerBackup()
        {
            BackupInvalido = false;
            if (!File.Exists(ArquivoBackup)) return new Backup();
            try
            {
                string texto = File.ReadAllText(ArquivoBackup, Encoding.UTF8);
                var serial = new JavaScriptSerializer();

                // Campo ausente viraria "false" ao desserializar, e isso apagaria um
                // valor que existia. Então "Existia" precisa estar escrito em todo item.
                var bruto = serial.DeserializeObject(texto) as Dictionary<string, object>;
                if (bruto == null) throw new InvalidDataException("formato inesperado");
                object lista;
                if (bruto.TryGetValue("Registro", out lista) && lista != null)
                {
                    var itensBrutos = lista as System.Collections.IEnumerable;
                    if (itensBrutos == null || lista is string) throw new InvalidDataException("lista de registro inválida");
                    foreach (var ib in itensBrutos)
                    {
                        var d = ib as Dictionary<string, object>;
                        if (d == null || !d.ContainsKey("Existia") || !(d["Existia"] is bool))
                            throw new InvalidDataException("item sem o campo Existia");
                    }
                }

                var b = serial.Deserialize<Backup>(texto);
                if (b == null) throw new InvalidDataException("arquivo vazio");
                if (b.Registro == null) b.Registro = new List<ItemBackup>();
                if (b.ChavesCriadas == null) b.ChavesCriadas = new List<string>();
                foreach (var i in b.Registro)
                {
                    if (i == null || string.IsNullOrEmpty(i.Id) || (i.Raiz != "HKCU" && i.Raiz != "HKLM") ||
                        string.IsNullOrEmpty(i.Caminho) || string.IsNullOrEmpty(i.Nome))
                        throw new InvalidDataException("item de backup incompleto");
                    if (i.Existia)
                    {
                        RegistryValueKind tipo;
                        if (!Enum.TryParse(i.Tipo, out tipo) || !Enum.IsDefined(typeof(RegistryValueKind), tipo) || tipo == RegistryValueKind.Unknown)
                            throw new InvalidDataException("tipo inválido em " + i.Id);
                        Decodificar(i.Valor, tipo);   // lança se o valor guardado estiver corrompido
                    }
                }
                foreach (var c in b.ChavesCriadas)
                    if (c == null || !(c.StartsWith(@"HKCU\") || c.StartsWith(@"HKLM\")))
                        throw new InvalidDataException("chave inválida no backup");
                return b;
            }
            catch (Exception e)
            {
                BackupInvalido = true;
                Log("Backup ilegível em " + ArquivoBackup + ": " + e.Message);
                return new Backup();
            }
        }

        void GarantirBackupValido()
        {
            if (BackupInvalido)
                throw new InvalidOperationException("O arquivo de backup está danificado (" + ArquivoBackup + "). Nada foi alterado.");
        }

        // Grava em disco primeiro; só então o novo estado passa a valer em memória.
        void SalvarBackup(Backup novo)
        {
            Directory.CreateDirectory(PastaDados);
            string temporario = ArquivoBackup + ".tmp";
            File.WriteAllText(temporario, new JavaScriptSerializer().Serialize(novo), Encoding.UTF8);
            if (File.Exists(ArquivoBackup)) File.Replace(temporario, ArquivoBackup, null);
            else File.Move(temporario, ArquivoBackup);
            Backup = novo;
        }

        public void MarcarOtimizado()
        {
            lock (trava)
            {
                if (BackupInvalido) return;
                var novo = Backup.Copia();
                novo.UltimaOtimizacao = DateTime.Now.ToString("dd/MM/yyyy 'às' HH:mm", new CultureInfo("pt-BR"));
                try { SalvarBackup(novo); } catch (Exception e) { Log("Não salvou a data: " + e.Message); }
            }
        }

        // ------------------------------------------------------------ registro

        static RegistryKey AbrirRaiz(string raiz)
        {
            var hive = raiz == "HKLM" ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;
            var visao = Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default;
            return RegistryKey.OpenBaseKey(hive, visao);
        }

        static string Codificar(object valor, RegistryValueKind tipo)
        {
            switch (tipo)
            {
                case RegistryValueKind.DWord: return Convert.ToInt32(valor).ToString(CultureInfo.InvariantCulture);
                case RegistryValueKind.QWord: return Convert.ToInt64(valor).ToString(CultureInfo.InvariantCulture);
                case RegistryValueKind.String:
                case RegistryValueKind.ExpandString: return (string)valor;
                case RegistryValueKind.MultiString: return new JavaScriptSerializer().Serialize((string[])valor);
                default: return Convert.ToBase64String(valor as byte[] ?? new byte[0]);
            }
        }

        static object Decodificar(string texto, RegistryValueKind tipo)
        {
            switch (tipo)
            {
                case RegistryValueKind.DWord: return int.Parse(texto, CultureInfo.InvariantCulture);
                case RegistryValueKind.QWord: return long.Parse(texto, CultureInfo.InvariantCulture);
                case RegistryValueKind.String:
                case RegistryValueKind.ExpandString: return texto ?? "";
                case RegistryValueKind.MultiString: return new JavaScriptSerializer().Deserialize<string[]>(texto);
                default: return Convert.FromBase64String(texto ?? "");
            }
        }

        static bool TemValor(RegistryKey k, string nome)
        {
            return k != null && k.GetValueNames().Contains(nome, StringComparer.OrdinalIgnoreCase);
        }

        // Grava um valor guardando antes o original (só na primeira vez, para que
        // otimizar de novo não sobrescreva o backup verdadeiro). Também anota as
        // chaves que precisou criar, para o "Desfazer" removê-las.
        void DefinirRegistro(string grupo, string raiz, string caminho, string nome, object valor, RegistryValueKind tipo)
        {
            lock (trava)
            {
                GarantirBackupValido();
                string id = raiz + "\\" + caminho + "|" + nome;
                ItemBackup novoItem = null;

                using (var r = AbrirRaiz(raiz))
                {
                    if (!Backup.Registro.Any(i => string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase)))
                    {
                        novoItem = new ItemBackup { Id = id, Grupo = grupo, Raiz = raiz, Caminho = caminho, Nome = nome };
                        using (var k = r.OpenSubKey(caminho))
                        {
                            if (TemValor(k, nome))
                            {
                                var original = k.GetValueKind(nome);
                                novoItem.Existia = true;
                                novoItem.Tipo = original.ToString();
                                novoItem.Valor = Codificar(k.GetValue(nome, null, RegistryValueOptions.DoNotExpandEnvironmentNames), original);
                            }
                        }
                        if (raiz == "HKLM")
                        {
                            var outro = ItemDeOutraConta(id);
                            if (outro != null) { novoItem.Existia = outro.Existia; novoItem.Tipo = outro.Tipo; novoItem.Valor = outro.Valor; }
                        }
                    }

                    var faltando = new List<string>();
                    string p = caminho;
                    while (!string.IsNullOrEmpty(p))
                    {
                        using (var k = r.OpenSubKey(p)) { if (k != null) break; }
                        faltando.Add(raiz + "\\" + p);
                        int corte = p.LastIndexOf('\\');
                        p = corte < 0 ? "" : p.Substring(0, corte);
                    }
                    var novasChaves = faltando
                        .Where(c => !Backup.ChavesCriadas.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();

                    if (novoItem != null || novasChaves.Count > 0)
                    {
                        var novo = Backup.Copia();
                        if (novoItem != null) novo.Registro.Add(novoItem);
                        novo.ChavesCriadas.AddRange(novasChaves);
                        SalvarBackup(novo);
                    }

                    using (var k = r.CreateSubKey(caminho))
                    {
                        if (k == null) throw new IOException("Não foi possível abrir " + caminho);
                        k.SetValue(nome, valor, tipo);
                    }
                }
            }
        }

        // HKLM e o plano de energia valem para o PC inteiro. Se outra conta do Windows
        // já otimizou, o valor atual não é o original: usa o que aquela conta guardou.
        List<Backup> BackupsDeOutrasContas()
        {
            var lista = new List<Backup>();
            try
            {
                foreach (var arquivo in Directory.GetFiles(PastaDados, "backup-*.json"))
                {
                    if (string.Equals(arquivo, ArquivoBackup, StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        var b = new JavaScriptSerializer().Deserialize<Backup>(File.ReadAllText(arquivo, Encoding.UTF8));
                        if (b != null) lista.Add(b);
                    }
                    catch { }
                }
            }
            catch { }
            return lista;
        }

        ItemBackup ItemDeOutraConta(string id)
        {
            foreach (var b in BackupsDeOutrasContas())
                foreach (var i in b.Registro ?? new List<ItemBackup>())
                {
                    if (i == null || !string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!i.Existia) return i;
                    try
                    {
                        Decodificar(i.Valor, (RegistryValueKind)Enum.Parse(typeof(RegistryValueKind), i.Tipo));
                        return i;
                    }
                    catch { }
                }
            return null;
        }

        static object LerValor(string raiz, string caminho, string nome)
        {
            try
            {
                using (var r = AbrirRaiz(raiz))
                using (var k = r.OpenSubKey(caminho))
                    return k == null ? null : k.GetValue(nome);
            }
            catch { return null; }
        }

        static string LerTexto(string raiz, string caminho, string nome)
        {
            object v = LerValor(raiz, caminho, nome);
            return v == null ? null : Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        bool RestaurarItem(ItemBackup item)
        {
            try
            {
                using (var r = AbrirRaiz(item.Raiz))
                {
                    if (item.Existia)
                    {
                        var tipo = (RegistryValueKind)Enum.Parse(typeof(RegistryValueKind), item.Tipo);
                        using (var k = r.CreateSubKey(item.Caminho))
                            k.SetValue(item.Nome, Decodificar(item.Valor, tipo), tipo);
                    }
                    else
                    {
                        using (var k = r.OpenSubKey(item.Caminho, true))
                            if (TemValor(k, item.Nome)) k.DeleteValue(item.Nome);
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                Log("Falha ao restaurar " + item.Id + ": " + e.Message);
                return false;
            }
        }

        // Remove as chaves que o FRAMEUP criou e que ficaram vazias (da mais funda
        // para a mais rasa) e tira do backup as que não existem mais. Devolve as falhas.
        int LimparChavesCriadas()
        {
            int falhas = 0;
            var restantes = new List<string>();
            foreach (var c in Backup.ChavesCriadas.OrderByDescending(x => x.Length))
            {
                try
                {
                    string raiz = c.Substring(0, 4);
                    string caminho = c.Substring(5);
                    bool existe, vazia;
                    using (var r = AbrirRaiz(raiz))
                    using (var k = r.OpenSubKey(caminho))
                    {
                        existe = k != null;
                        vazia = existe && k.ValueCount == 0 && k.SubKeyCount == 0;
                    }
                    if (!existe) continue;
                    if (!vazia) { restantes.Add(c); continue; }

                    int corte = caminho.LastIndexOf('\\');
                    string pai = corte < 0 ? "" : caminho.Substring(0, corte);
                    string filho = corte < 0 ? caminho : caminho.Substring(corte + 1);
                    using (var r = AbrirRaiz(raiz))
                    {
                        if (pai.Length == 0) r.DeleteSubKey(filho, false);
                        else using (var kp = r.OpenSubKey(pai, true)) kp.DeleteSubKey(filho, false);
                    }
                }
                catch (Exception e)
                {
                    falhas++;
                    restantes.Add(c);
                    Log("Falha ao remover a chave " + c + ": " + e.Message);
                }
            }
            if (restantes.Count != Backup.ChavesCriadas.Count)
            {
                var novo = Backup.Copia();
                novo.ChavesCriadas = restantes;
                try { SalvarBackup(novo); } catch (Exception e) { falhas++; Log("Não salvou o backup: " + e.Message); }
            }
            return falhas;
        }

        Resultado RestaurarRegistro()
        {
            lock (trava)
            {
                var itens = Backup.Registro.ToList();
                int falhas = itens.Count(i => !RestaurarItem(i));
                falhas += LimparChavesCriadas();
                if (falhas > 0)
                    return Resultado.Erro(falhas + " itens não voltaram. Tente de novo; detalhes em " + ArquivoLog);
                return Resultado.Ok(itens.Count + " configurações devolvidas ao valor original.");
            }
        }

        public bool TemBackupDoGrupo(string grupo)
        {
            return Backup.Registro.Any(i => i.Grupo == grupo);
        }

        // Desfaz um ajuste só: devolve os valores dele e tira esses itens do backup.
        public Resultado DesfazerGrupo(string grupo)
        {
            lock (trava)
            {
                GarantirBackupValido();
                var itens = Backup.Registro.Where(i => i.Grupo == grupo).ToList();
                if (itens.Count == 0) return Resultado.Aviso("Nada para desfazer neste ajuste.");
                var voltaram = itens.Where(RestaurarItem).ToList();
                var novo = Backup.Copia();
                novo.Registro.RemoveAll(i => voltaram.Contains(i));
                SalvarBackup(novo);
                int falhas = itens.Count - voltaram.Count + LimparChavesCriadas();
                if (falhas > 0) return Resultado.Erro("Parte do ajuste não voltou. Tente de novo; detalhes em " + ArquivoLog);
                return Resultado.Ok("Voltou ao valor original.");
            }
        }

        // ------------------------------------------------------------ comandos do Windows

        static string Executar(string programa, string argumentos, out int codigo)
        {
            var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, programa), argumentos)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (var p = Process.Start(psi))
            {
                var saida = p.StandardOutput.ReadToEndAsync();
                var erro = p.StandardError.ReadToEndAsync();
                // Um comando travado não pode prender o programa: desiste depois de 20 s.
                if (!p.WaitForExit(20000))
                {
                    try { p.Kill(); } catch { }
                    codigo = -1;
                    return "";
                }
                p.WaitForExit();
                codigo = p.ExitCode;
                return saida.Result;
            }
        }

        internal static string PlanoAtivo()
        {
            int codigo;
            var m = Regex.Match(Executar("powercfg.exe", "/getactivescheme", out codigo),
                @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
            return m.Success ? m.Value.ToLowerInvariant() : null;
        }

        static bool PlanoExiste(string guid)
        {
            int codigo;
            return Executar("powercfg.exe", "/list", out codigo).IndexOf(guid, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Consulta WMI com prazo: se o serviço do Windows travar, desiste em vez de esperar para sempre.
        internal static ManagementObjectSearcher Buscar(string consulta)
        {
            return new ManagementObjectSearcher(new ObjectQuery(consulta))
            {
                Options = new EnumerationOptions { Timeout = TimeSpan.FromSeconds(10), ReturnImmediately = true, Rewindable = false }
            };
        }

        internal static List<string> Wmi(string consulta, string propriedade)
        {
            var lista = new List<string>();
            try
            {
                using (var s = Buscar(consulta))
                    foreach (ManagementBaseObject o in s.Get())
                    {
                        object v = o[propriedade];
                        if (v != null) lista.Add(Convert.ToString(v, CultureInfo.InvariantCulture).Trim());
                    }
            }
            catch { }
            return lista;
        }

        public static void Abrir(string endereco)
        {
            // Pelo Explorer, o site/configuração abre sem herdar o modo administrador.
            Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
                "\"" + endereco + "\"") { UseShellExecute = false });
        }

        // ------------------------------------------------------------ informações

        public static InfoPC LerInfoPC()
        {
            var info = new InfoPC();
            var cpu = Wmi("SELECT Name FROM Win32_Processor", "Name");
            if (cpu.Count > 0) info.Cpu = Regex.Replace(cpu[0], @"\s+", " ");
            var gpus = Wmi("SELECT Name FROM Win32_VideoController", "Name")
                .Where(n => !Regex.IsMatch(n, "Basic|Remote|Virtual|Mirror|Parsec|Meta", RegexOptions.IgnoreCase)).ToList();
            if (gpus.Count > 0) info.Gpu = string.Join(" + ", gpus);
            var ram = Wmi("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem", "TotalPhysicalMemory");
            ulong bytes;
            if (ram.Count > 0 && ulong.TryParse(ram[0], out bytes)) info.Ram = Math.Round(bytes / 1073741824.0) + " GB";
            var so = Wmi("SELECT Caption FROM Win32_OperatingSystem", "Caption");
            if (so.Count > 0) info.Windows = Regex.Replace(so[0], @"^Microsoft\s+", "");
            return info;
        }

        public static bool TemBateria()
        {
            return Wmi("SELECT Name FROM Win32_Battery", "Name").Count > 0;
        }

        // ------------------------------------------------------------ segurança

        public Resultado CriarPontoRestauracao()
        {
            try
            {
                using (var mc = new ManagementClass(@"\\.\root\default", "SystemRestore", null))
                {
                    var entrada = mc.GetMethodParameters("CreateRestorePoint");
                    entrada["Description"] = "FRAMEUP - antes da otimizacao";
                    entrada["RestorePointType"] = 12;   // MODIFY_SETTINGS
                    entrada["EventType"] = 100;         // BEGIN_SYSTEM_CHANGE
                    var saida = mc.InvokeMethod("CreateRestorePoint", entrada, null);
                    uint retorno = Convert.ToUInt32(saida["ReturnValue"]);
                    if (retorno == 0) return Resultado.Ok("Ponto de restauração do Windows disponível.");
                    Log("CreateRestorePoint retornou " + retorno);
                }
            }
            catch (Exception e) { Log("Ponto de restauração: " + e.Message); }
            return new Resultado(Status.Aviso,
                "Ponto não criado (Proteção do Sistema desligada?). O backup do FRAMEUP vale mesmo assim.", "SEM_PROTECAO");
        }

        public Resultado AtivarProtecaoSistema()
        {
            using (var mc = new ManagementClass(@"\\.\root\default", "SystemRestore", null))
            {
                var entrada = mc.GetMethodParameters("Enable");
                entrada["Drive"] = Path.GetPathRoot(Environment.SystemDirectory);
                var saida = mc.InvokeMethod("Enable", entrada, null);
                uint retorno = Convert.ToUInt32(saida["ReturnValue"]);
                if (retorno != 0) return Resultado.Erro("O Windows recusou ativar a Proteção do Sistema (código " + retorno + ").");
            }
            return Resultado.Ok("Proteção do Sistema ativada.");
        }

        // ------------------------------------------------------------ energia

        public Resultado AtivarPlanoDesempenho()
        {
            lock (trava)
            {
                GarantirBackupValido();
                string ativo = PlanoAtivo();
                if (ativo == null) return Resultado.Aviso("Não consegui ler o plano de energia atual, então ele não foi alterado.");
                if (string.IsNullOrEmpty(Backup.PlanoOriginal))
                {
                    string original = ativo != GuidPlanoFrameup ? ativo
                        : BackupsDeOutrasContas().Select(b => b.PlanoOriginal).FirstOrDefault(x => !string.IsNullOrEmpty(x));
                    if (original != null)
                    {
                        var novo = Backup.Copia();
                        novo.PlanoOriginal = original;
                        SalvarBackup(novo);
                    }
                }
            }

            int codigo;
            if (!PlanoExiste(GuidPlanoFrameup))
            {
                Executar("powercfg.exe", "/duplicatescheme " + GuidAltoDesempenho + " " + GuidPlanoFrameup, out codigo);
                if (codigo != 0 || !PlanoExiste(GuidPlanoFrameup))
                    return Resultado.Aviso("Este PC não oferece Alto Desempenho (comum em notebooks). Use Configurações > Energia > Melhor desempenho.");
                Executar("powercfg.exe", "/changename " + GuidPlanoFrameup + " \"FRAMEUP Desempenho\" \"Alto desempenho criado pelo FRAMEUP\"", out codigo);
            }
            Executar("powercfg.exe", "/setactive " + GuidPlanoFrameup, out codigo);
            if (codigo != 0) return Resultado.Erro("Não foi possível ativar o plano de energia.");

            if (TemBateria()) return Resultado.Ok("Ativo. Notebook: jogue na tomada, na bateria ele gasta mais.");
            return Resultado.Ok("Plano \"FRAMEUP Desempenho\" ativo.");
        }

        // Lê o "estado mínimo do processador" do plano ativo (na tomada). Em planos de
        // desempenho ele é 100%; no Equilibrado, 5%. Vale até para planos personalizados.
        // Devolve null se não deu para ler.
        public static bool? ProcessadorSemEconomia()
        {
            int codigo;
            var valores = Regex.Matches(Executar("powercfg.exe", "/query SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN", out codigo),
                @"0x([0-9a-fA-F]{8})");
            if (codigo != 0 || valores.Count < 2) return null;
            int naTomada = Convert.ToInt32(valores[valores.Count - 2].Groups[1].Value, 16);
            return naTomada >= 100;
        }

        public static bool PlanoFrameupAtivo()
        {
            return PlanoAtivo() == GuidPlanoFrameup;
        }

        Resultado RestaurarPlano()
        {
            int codigo;
            string original = Backup.PlanoOriginal;
            bool voltou = false;
            if (!string.IsNullOrEmpty(original) && PlanoExiste(original))
            {
                Executar("powercfg.exe", "/setactive " + original, out codigo);
                voltou = codigo == 0;
                if (!voltou) return Resultado.Erro("Não foi possível reativar o plano de energia original.");
            }
            if (!voltou && PlanoAtivo() == GuidPlanoFrameup)
                Executar("powercfg.exe", "/setactive " + GuidEquilibrado, out codigo);
            if (PlanoExiste(GuidPlanoFrameup))
                Executar("powercfg.exe", "/delete " + GuidPlanoFrameup, out codigo);

            if (PlanoAtivo() == GuidPlanoFrameup) return Resultado.Erro("O plano do FRAMEUP continua ativo.");
            if (PlanoExiste(GuidPlanoFrameup))
                return Resultado.Aviso("O plano de energia voltou, mas o plano FRAMEUP ficou na lista. Dá para apagar em Opções de Energia.");
            if (!string.IsNullOrEmpty(original) && !voltou) return Resultado.Aviso("O plano original não existe mais; ativado o Equilibrado.");
            return Resultado.Ok("Plano de energia original ativo.");
        }

        public bool PodeDesfazerPlano()
        {
            return !string.IsNullOrEmpty(Backup.PlanoOriginal) || PlanoExiste(GuidPlanoFrameup);
        }

        public Resultado DesfazerPlano()
        {
            lock (trava)
            {
                GarantirBackupValido();
                var r = RestaurarPlano();
                if (r.Status != Status.Erro && !string.IsNullOrEmpty(Backup.PlanoOriginal))
                {
                    var novo = Backup.Copia();
                    novo.PlanoOriginal = null;
                    SalvarBackup(novo);
                }
                return r;
            }
        }

        // A suspensão de USB só é mudada dentro do plano do FRAMEUP: ao desfazer o
        // plano ela some junto, e o plano original do Windows nunca é tocado.
        public Resultado DesligarSuspensaoUsb()
        {
            if (!PlanoFrameupAtivo())
                return Resultado.Aviso("Precisa do plano FRAMEUP ativo. Marque também \"Plano de energia\".");
            int c1, c2, c3;
            Executar("powercfg.exe", "/setacvalueindex " + GuidPlanoFrameup + " " + SubgrupoUsb + " " + SuspensaoUsb + " 0", out c1);
            Executar("powercfg.exe", "/setdcvalueindex " + GuidPlanoFrameup + " " + SubgrupoUsb + " " + SuspensaoUsb + " 0", out c2);
            Executar("powercfg.exe", "/setactive " + GuidPlanoFrameup, out c3);
            if (c1 != 0 || c2 != 0) return Resultado.Aviso("O Windows não deixou mudar a economia de energia do USB neste PC.");
            return Resultado.Ok("Mouse, teclado e headset USB não entram mais em economia de energia.");
        }

        // Valores na tomada e na bateria da suspensão de USB do plano FRAMEUP, ou null.
        static int[] SuspensaoUsbAtual()
        {
            if (!PlanoExiste(GuidPlanoFrameup)) return null;
            int codigo;
            var valores = Regex.Matches(Executar("powercfg.exe", "/query " + GuidPlanoFrameup + " " + SubgrupoUsb + " " + SuspensaoUsb, out codigo),
                @"0x([0-9a-fA-F]{8})");
            if (codigo != 0 || valores.Count < 2) return null;
            return new[] { Convert.ToInt32(valores[valores.Count - 2].Groups[1].Value, 16), Convert.ToInt32(valores[valores.Count - 1].Groups[1].Value, 16) };
        }

        public static bool UsbSemSuspensao()
        {
            var v = SuspensaoUsbAtual();
            return v != null && v[0] == 0 && v[1] == 0;
        }

        // Basta um dos dois estar mudado para o "Desfazer" aparecer (falha parcial).
        public static bool UsbAlterado()
        {
            var v = SuspensaoUsbAtual();
            return v != null && (v[0] == 0 || v[1] == 0);
        }

        public Resultado ReativarSuspensaoUsb()
        {
            if (!PlanoExiste(GuidPlanoFrameup)) return Resultado.Ok("Voltou ao padrão do Windows.");
            int c1, c2, c3;
            Executar("powercfg.exe", "/setacvalueindex " + GuidPlanoFrameup + " " + SubgrupoUsb + " " + SuspensaoUsb + " 1", out c1);
            Executar("powercfg.exe", "/setdcvalueindex " + GuidPlanoFrameup + " " + SubgrupoUsb + " " + SuspensaoUsb + " 1", out c2);
            if (PlanoFrameupAtivo()) Executar("powercfg.exe", "/setactive " + GuidPlanoFrameup, out c3);
            if (c1 != 0 || c2 != 0) return Resultado.Erro("Não foi possível voltar a economia de energia do USB.");
            return Resultado.Ok("Voltou ao padrão do Windows.");
        }

        // ------------------------------------------------------------ jogos e Windows

        public Resultado LigarModoDeJogo()
        {
            DefinirRegistro("modojogo", "HKCU", @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1, RegistryValueKind.DWord);
            DefinirRegistro("modojogo", "HKCU", @"Software\Microsoft\GameBar", "AllowAutoGameMode", 1, RegistryValueKind.DWord);
            return Resultado.Ok("O Windows passa a priorizar o jogo que está aberto.");
        }

        public static bool ModoDeJogoLigado()
        {
            // Sem o valor, o Windows 10/11 deixa o Modo de Jogo ligado por padrão.
            string v = LerTexto("HKCU", @"Software\Microsoft\GameBar", "AutoGameModeEnabled");
            return v == null || v == "1";
        }

        public Resultado DesligarGravacaoFundo()
        {
            DefinirRegistro("gravacao", "HKCU", @"System\GameConfigStore", "GameDVR_Enabled", 0, RegistryValueKind.DWord);
            DefinirRegistro("gravacao", "HKCU", @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0, RegistryValueKind.DWord);
            return Resultado.Ok("Gravação do Xbox Game Bar desligada. OBS e ShadowPlay continuam funcionando.");
        }

        public static bool GravacaoDesligada()
        {
            return LerTexto("HKCU", @"System\GameConfigStore", "GameDVR_Enabled") == "0";
        }

        public Resultado PriorizarJogos()
        {
            DefinirRegistro("prioridade", "HKLM", Perfil, "SystemResponsiveness", 10, RegistryValueKind.DWord);
            DefinirRegistro("prioridade", "HKLM", PerfilJogos, "GPU Priority", 8, RegistryValueKind.DWord);
            DefinirRegistro("prioridade", "HKLM", PerfilJogos, "Priority", 6, RegistryValueKind.DWord);
            DefinirRegistro("prioridade", "HKLM", PerfilJogos, "Scheduling Category", "High", RegistryValueKind.String);
            DefinirRegistro("prioridade", "HKLM", PerfilJogos, "SFIO Priority", "High", RegistryValueKind.String);
            return Resultado.Ok("CPU e GPU dão preferência ao jogo sobre tarefas de fundo.");
        }

        public static bool PrioridadeAtiva()
        {
            return LerTexto("HKLM", Perfil, "SystemResponsiveness") == "10" && LerTexto("HKLM", PerfilJogos, "Priority") == "6";
        }

        public Resultado AliviarVisual()
        {
            DefinirRegistro("visual", "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0, RegistryValueKind.DWord);
            DefinirRegistro("visual", "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations", 0, RegistryValueKind.DWord);
            DefinirRegistro("visual", "HKCU", @"Control Panel\Desktop\WindowMetrics", "MinAnimate", "0", RegistryValueKind.String);
            return Resultado.Ok("Menos transparência e animações. As fontes continuam suaves.");
        }

        public static bool VisualLeveAtivo()
        {
            return LerTexto("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency") == "0"
                && LerTexto("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations") == "0";
        }

        public Resultado DesligarSugestoes()
        {
            foreach (var nome in Sugestoes)
                DefinirRegistro("dicas", "HKCU", Conteudo, nome, 0, RegistryValueKind.DWord);
            return Resultado.Ok("Sem dicas, sugestões e apps instalados sozinhos pelo Windows.");
        }

        public static bool SugestoesDesligadas()
        {
            return LerTexto("HKCU", Conteudo, "SubscribedContent-338389Enabled") == "0"
                && LerTexto("HKCU", Conteudo, "SystemPaneSuggestionsEnabled") == "0";
        }

        public Resultado EfeitosDesempenho()
        {
            DefinirRegistro("efeitos", "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 2, RegistryValueKind.DWord);
            DefinirRegistro("efeitos", "HKCU", @"Control Panel\Desktop", "UserPreferencesMask",
                new byte[] { 0x90, 0x12, 0x03, 0x80, 0x10, 0x00, 0x00, 0x00 }, RegistryValueKind.Binary);
            DefinirRegistro("efeitos", "HKCU", @"Control Panel\Desktop", "FontSmoothing", "2", RegistryValueKind.String);
            return Resultado.Ok("Efeitos visuais no modo desempenho, com fontes suaves. Vale depois de reiniciar.");
        }

        public static bool EfeitosDesempenhoAtivos()
        {
            return LerTexto("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting") == "2";
        }

        public Resultado DesligarAceleracaoMouse()
        {
            DefinirRegistro("mouse", "HKCU", @"Control Panel\Mouse", "MouseSpeed", "0", RegistryValueKind.String);
            DefinirRegistro("mouse", "HKCU", @"Control Panel\Mouse", "MouseThreshold1", "0", RegistryValueKind.String);
            DefinirRegistro("mouse", "HKCU", @"Control Panel\Mouse", "MouseThreshold2", "0", RegistryValueKind.String);
            return Resultado.Ok("O cursor anda só o quanto o mouse anda. Vale depois de reiniciar.");
        }

        public static bool MouseSemAceleracao()
        {
            return LerTexto("HKCU", @"Control Panel\Mouse", "MouseSpeed") == "0";
        }

        public Resultado TecladoRapido()
        {
            DefinirRegistro("teclado", "HKCU", @"Control Panel\Keyboard", "KeyboardDelay", "0", RegistryValueKind.String);
            DefinirRegistro("teclado", "HKCU", @"Control Panel\Keyboard", "KeyboardSpeed", "31", RegistryValueKind.String);
            return Resultado.Ok("Tecla segurada repete mais rápido. Vale depois de reiniciar.");
        }

        public static bool TecladoRapidoAtivo()
        {
            return LerTexto("HKCU", @"Control Panel\Keyboard", "KeyboardDelay") == "0"
                && LerTexto("HKCU", @"Control Panel\Keyboard", "KeyboardSpeed") == "31";
        }

        public Resultado DesligarAppsSegundoPlano()
        {
            DefinirRegistro("appsfundo", "HKCU", @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1, RegistryValueKind.DWord);
            DefinirRegistro("appsfundo", "HKCU", @"Software\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle", 0, RegistryValueKind.DWord);
            return Resultado.Ok("Apps da Microsoft Store não rodam escondidos. Avisos deles podem atrasar.");
        }

        public static bool AppsSegundoPlanoDesligados()
        {
            return LerTexto("HKCU", @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled") == "1";
        }

        public Resultado RedeSemLimite()
        {
            DefinirRegistro("rede", "HKLM", Perfil, "NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), RegistryValueKind.DWord);
            return Resultado.Ok("O Windows não limita mais os pacotes de rede enquanto toca áudio ou vídeo.");
        }

        public static bool RedeSemLimiteAtiva()
        {
            return LerTexto("HKLM", Perfil, "NetworkThrottlingIndex") == "-1";
        }

        public Resultado LimparCacheDns()
        {
            int codigo;
            Executar("ipconfig.exe", "/flushdns", out codigo);
            if (codigo != 0) return Resultado.Aviso("Não foi possível limpar o cache DNS.");
            return Resultado.Ok("Endereços de sites e servidores serão consultados de novo.");
        }

        // ------------------------------------------------------------ limpeza

        static string Local(string relativo)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), relativo);
        }

        static string Comum(string relativo)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), relativo);
        }

        // Só aceita pastas que se chamam "Temp", fora da raiz do disco. Se a
        // própria pasta Temp for um atalho (junction/symlink), ela é ignorada.
        public static bool PastaTempSegura(string caminho)
        {
            string completo;
            try { completo = Path.GetFullPath(caminho).TrimEnd('\\'); } catch { return false; }
            if (completo.Length <= 3) return false;
            if (!string.Equals(Path.GetFileName(completo), "Temp", StringComparison.OrdinalIgnoreCase)) return false;
            var pasta = new DirectoryInfo(completo);
            if (!pasta.Exists) return false;
            return (pasta.Attributes & FileAttributes.ReparsePoint) == 0;
        }

        // Limpa o conteúdo de cada pasta cujo caminho real termina exatamente como
        // esperado (ex.: "\WER\ReportQueue"). Pasta que não existe é ignorada.
        long LimparPastas(string[] pastas, string[] finais, out int limpas)
        {
            string protegida = Limpador.RaizReal(pastaDoPrograma) ?? pastaDoPrograma;
            long bytes = 0;
            limpas = 0;
            for (int i = 0; i < pastas.Length; i++)
            {
                if (!Directory.Exists(pastas[i])) continue;
                string raiz = Limpador.RaizReal(pastas[i]);
                if (raiz == null || raiz.Length <= 3 || !raiz.EndsWith(finais[i], StringComparison.OrdinalIgnoreCase))
                {
                    Log("Pasta ignorada por segurança: " + pastas[i]);
                    continue;
                }
                limpas++;
                bytes += Limpador.LimparConteudo(raiz, protegida);
            }
            return bytes;
        }

        string Mb(long bytes)
        {
            return string.Format(new CultureInfo("pt-BR"), "{0:N0} MB", bytes / 1048576.0);
        }

        public Resultado LimparTemporarios(out long bytesLiberados)
        {
            var pastas = new List<string>();
            var finais = new List<string>();
            var temps = new[] { Local("Temp"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp") };
            foreach (var t in temps.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (PastaTempSegura(t)) { pastas.Add(t); finais.Add(@"\Temp"); }
                else Log("Pasta ignorada por segurança: " + t);
            }
            // Relatórios de erro antigos do Windows.
            pastas.Add(Comum(@"Microsoft\Windows\WER\ReportArchive")); finais.Add(@"\WER\ReportArchive");
            pastas.Add(Comum(@"Microsoft\Windows\WER\ReportQueue")); finais.Add(@"\WER\ReportQueue");
            int limpas;
            bytesLiberados = LimparPastas(pastas.ToArray(), finais.ToArray(), out limpas);
            if (limpas == 0) return Resultado.Aviso("Nenhuma pasta temporária reconhecida; nada foi apagado.");
            return Resultado.Ok(Mb(bytesLiberados) + " liberados. Arquivos em uso foram mantidos.");
        }

        public Resultado LimparShaders(out long bytesLiberados)
        {
            var pastas = new[] { Local("D3DSCache"), Local(@"NVIDIA\DXCache"), Local(@"NVIDIA\GLCache"), Local(@"AMD\DxCache"), Local(@"AMD\GLCache") };
            var finais = new[] { @"\D3DSCache", @"\NVIDIA\DXCache", @"\NVIDIA\GLCache", @"\AMD\DxCache", @"\AMD\GLCache" };
            int limpas;
            bytesLiberados = LimparPastas(pastas, finais, out limpas);
            if (limpas == 0) return Resultado.Ok("Nenhum cache de shaders encontrado.");
            return Resultado.Ok(Mb(bytesLiberados) + " liberados. Os jogos refazem o cache na primeira vez que abrirem.");
        }

        // SHQUERYRBINFO tem 24 bytes no Windows 64 bits e 20 (compactado) no de 32 bits.
        [StructLayout(LayoutKind.Sequential)]
        struct InfoLixeira { public int Tamanho; public long Bytes; public long Itens; }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct InfoLixeira32 { public int Tamanho; public long Bytes; public long Itens; }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHQueryRecycleBinW(string raiz, ref InfoLixeira info);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHQueryRecycleBinW")]
        static extern int SHQueryRecycleBinW32(string raiz, ref InfoLixeira32 info);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHEmptyRecycleBinW(IntPtr janela, string raiz, uint flags);

        public static long TamanhoLixeira()
        {
            if (IntPtr.Size == 4)
            {
                var info32 = new InfoLixeira32 { Tamanho = Marshal.SizeOf(typeof(InfoLixeira32)) };
                return SHQueryRecycleBinW32(null, ref info32) == 0 ? info32.Bytes : -1;
            }
            var info = new InfoLixeira { Tamanho = Marshal.SizeOf(typeof(InfoLixeira)) };
            return SHQueryRecycleBinW(null, ref info) == 0 ? info.Bytes : -1;
        }

        public Resultado EsvaziarLixeira(out long bytesLiberados)
        {
            bytesLiberados = Math.Max(0, TamanhoLixeira());
            if (bytesLiberados == 0) return Resultado.Ok("A lixeira já estava vazia.");
            const uint semConfirmar = 0x1, semProgresso = 0x2, semSom = 0x4;
            int r = SHEmptyRecycleBinW(IntPtr.Zero, null, semConfirmar | semProgresso | semSom);
            if (r != 0 && r != unchecked((int)0x8000FFFF))
            {
                bytesLiberados = 0;
                return Resultado.Aviso("O Windows não esvaziou a lixeira (código " + r + ").");
            }
            return Resultado.Ok(Mb(bytesLiberados) + " liberados da lixeira.");
        }

        // ------------------------------------------------------------ inicialização e desfazer

        public static List<string> ProgramasNaInicializacao()
        {
            return Wmi("SELECT Name FROM Win32_StartupCommand", "Name")
                .Where(n => n.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public List<Resultado> DesfazerTudo()
        {
            if (BackupInvalido)
                return new List<Resultado> { Resultado.Erro("O arquivo de backup está danificado. Use a Restauração do Sistema do Windows.") };

            var resultados = new List<Resultado> { RestaurarRegistro(), RestaurarPlano() };
            if (resultados.All(r => r.Status != Status.Erro))
            {
                lock (trava)
                {
                    try
                    {
                        if (File.Exists(ArquivoBackup))
                            File.Move(ArquivoBackup, ArquivoBackup + ".desfeito-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                    }
                    catch (Exception e)
                    {
                        Log("Não arquivou o backup: " + e.Message);
                        resultados.Add(Resultado.Erro("As configurações voltaram, mas o backup não pôde ser arquivado. Clique em DESFAZER TUDO de novo."));
                    }
                    if (resultados.All(r => r.Status != Status.Erro)) Backup = new Backup();
                }
            }
            foreach (var r in resultados) Log("Desfazer: " + r.Status + " | " + r.Detalhe);
            return resultados;
        }

        public static void Reiniciar()
        {
            int codigo;
            Executar("shutdown.exe", "/r /t 15 /c \"FRAMEUP: reiniciando em 15 segundos para aplicar a otimização.\"", out codigo);
        }

        public static void AbrirGerenciadorDeTarefas()
        {
            Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "taskmgr.exe"), "/0 /startup") { UseShellExecute = true });
        }
    }
}
