// Limpeza de pastas Temp sem nunca seguir atalhos (junction/symlink).
//
// Cada item é aberto SEM seguir reparse points, o caminho real do objeto aberto
// é conferido e a exclusão é feita pelo próprio identificador (handle). Assim,
// mesmo que outro programa troque uma pasta por um atalho no meio da limpeza,
// nada fora da pasta Temp é apagado.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Frameup
{
    public static class Limpador
    {
        const uint DELETE = 0x00010000, LER_ATRIBUTOS = 0x80, GRAVAR_ATRIBUTOS = 0x100;
        const uint COMPARTILHAR_TUDO = 7, ABRIR_EXISTENTE = 3;
        const uint NAO_SEGUIR_ATALHO = 0x00200000, ABRIR_PASTAS = 0x02000000;
        const uint ATTR_SOMENTE_LEITURA = 0x1, ATTR_PASTA = 0x10, ATTR_NORMAL = 0x80, ATTR_ATALHO = 0x400;
        const int INFO_BASICA = 0, INFO_DISPOSICAO = 4;

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        struct InfoArquivo
        {
            public uint Atributos;
            public long Criado, Acesso, Escrita;
            public uint Volume, TamanhoAlto, TamanhoBaixo, Links, IndiceAlto, IndiceBaixo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct InfoBasica
        {
            public long Criado, Acesso, Escrita, Alterado;
            public uint Atributos;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct Disposicao
        {
            [MarshalAs(UnmanagedType.U1)] public bool Apagar;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafeFileHandle CreateFileW(string nome, uint acesso, uint compartilhar, IntPtr seguranca, uint criacao, uint flags, IntPtr modelo);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetFileInformationByHandle(SafeFileHandle h, out InfoArquivo info);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint GetFinalPathNameByHandleW(SafeFileHandle h, StringBuilder texto, uint tamanho, uint flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetFileInformationByHandle(SafeFileHandle h, int classe, ref InfoBasica info, uint tamanho);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetFileInformationByHandle(SafeFileHandle h, int classe, ref Disposicao info, uint tamanho);

        static SafeFileHandle Abrir(string caminho, uint acesso)
        {
            string completo = Path.GetFullPath(caminho).TrimEnd('\\');
            var h = CreateFileW(@"\\?\" + completo, acesso, COMPARTILHAR_TUDO, IntPtr.Zero, ABRIR_EXISTENTE,
                                NAO_SEGUIR_ATALHO | ABRIR_PASTAS, IntPtr.Zero);
            if (h.IsInvalid) { h.Dispose(); return null; }
            return h;
        }

        // Caminho real do objeto aberto. Caminhos de rede nunca são aceitos.
        static string CaminhoReal(SafeFileHandle h)
        {
            var texto = new StringBuilder(1024);
            uint n = GetFinalPathNameByHandleW(h, texto, (uint)texto.Capacity, 0);
            if (n >= texto.Capacity)
            {
                texto = new StringBuilder((int)n + 1);
                n = GetFinalPathNameByHandleW(h, texto, (uint)texto.Capacity, 0);
            }
            if (n == 0 || n >= texto.Capacity) return null;
            string s = texto.ToString();
            if (s.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) return null;
            if (s.StartsWith(@"\\?\")) s = s.Substring(4);
            return s.TrimEnd('\\');
        }

        // Caminho real de uma pasta que pode ser limpa, ou null se ela não
        // existe, é um atalho ou fica na rede.
        public static string RaizReal(string pasta)
        {
            try
            {
                using (var h = Abrir(pasta, LER_ATRIBUTOS))
                {
                    InfoArquivo i;
                    if (h == null || !GetFileInformationByHandle(h, out i)) return null;
                    if ((i.Atributos & ATTR_ATALHO) != 0 || (i.Atributos & ATTR_PASTA) == 0) return null;
                    return CaminhoReal(h);
                }
            }
            catch { return null; }
        }

        // Apaga o conteúdo da pasta (não a pasta em si) e devolve os bytes liberados.
        // "protegida" é a pasta do próprio programa, que nunca é apagada.
        public static long LimparConteudo(string raizReal, string protegida)
        {
            long total = 0;
            foreach (var item in Listar(raizReal)) total += Apagar(item, raizReal, protegida);
            return total;
        }

        static string[] Listar(string pasta)
        {
            try { return Directory.GetFileSystemEntries(pasta); }
            catch { return new string[0]; }
        }

        static long Apagar(string caminho, string raizReal, string protegida)
        {
            long bytes = 0;
            try
            {
                using (var h = Abrir(caminho, DELETE | LER_ATRIBUTOS | GRAVAR_ATRIBUTOS))
                {
                    if (h == null) return 0;   // em uso ou sem permissão: fica

                    string real = CaminhoReal(h);
                    if (real == null || !real.StartsWith(raizReal + "\\", StringComparison.OrdinalIgnoreCase)) return 0;
                    if (protegida != null && (protegida + "\\").StartsWith(real + "\\", StringComparison.OrdinalIgnoreCase)) return 0;

                    InfoArquivo i;
                    if (!GetFileInformationByHandle(h, out i)) return 0;
                    bool atalho = (i.Atributos & ATTR_ATALHO) != 0;
                    bool pasta = (i.Atributos & ATTR_PASTA) != 0;

                    // Entra só em pastas de verdade; de um atalho, apaga só o atalho.
                    if (pasta && !atalho)
                        foreach (var filho in Listar(real)) bytes += Apagar(filho, raizReal, protegida);

                    if ((i.Atributos & ATTR_SOMENTE_LEITURA) != 0)
                    {
                        uint semLeitura = i.Atributos & ~ATTR_SOMENTE_LEITURA;
                        var basica = new InfoBasica { Atributos = semLeitura == 0 ? ATTR_NORMAL : semLeitura };
                        SetFileInformationByHandle(h, INFO_BASICA, ref basica, (uint)Marshal.SizeOf(typeof(InfoBasica)));
                    }

                    var disposicao = new Disposicao { Apagar = true };
                    bool apagou = SetFileInformationByHandle(h, INFO_DISPOSICAO, ref disposicao, (uint)Marshal.SizeOf(typeof(Disposicao)));
                    if (apagou && !pasta) bytes += ((long)i.TamanhoAlto << 32) | i.TamanhoBaixo;
                }
            }
            catch { }
            return bytes;
        }
    }
}
