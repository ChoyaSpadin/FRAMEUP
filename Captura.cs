// Só entra na versão de captura (compilada com /define:CAPTURA): monta a tela
// em estados de exemplo e salva PNGs para o anúncio. Não altera nada no PC.
#if CAPTURA
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Frameup
{
    static class Captura
    {
        public static void Preparar(Tela t, string[] args)
        {
            string pasta = args.Length > 0 ? args[0] : ".";
            var j = t.Janela;
            j.WindowStartupLocation = WindowStartupLocation.Manual;
            j.Left = -4000;
            j.Top = 0;
            j.ShowInTaskbar = false;

            j.Loaded += async (s, e) =>
            {
                var info = Otimizador.LerInfoPC();
                t.Achar<TextBlock>("InfoCpu").Text = info.Cpu;
                t.Achar<TextBlock>("InfoGpu").Text = info.Gpu;
                t.Achar<TextBlock>("InfoRam").Text = info.Ram;
                t.Achar<TextBlock>("InfoSo").Text = info.Windows;
                t.Achar<TextBlock>("TextoStatus").Text = "Nenhuma alteração feita neste PC ainda.";
                await Task.Delay(500);
                Salvar(t, System.IO.Path.Combine(pasta, "app-inicio.png"));

                var detalhes = new Dictionary<string, string>
                {
                    { "ponto", "Ponto de restauração do Windows disponível." },
                    { "plano", "Plano \"FRAMEUP Desempenho\" ativo." },
                    { "usb", "Mouse, teclado e headset USB não entram mais em economia de energia." },
                    { "modojogo", "O Windows passa a priorizar o jogo que está aberto." },
                    { "gravacao", "Gravação do Xbox Game Bar desligada. OBS e ShadowPlay continuam funcionando." },
                    { "prioridade", "CPU e GPU dão preferência ao jogo sobre tarefas de fundo." },
                    { "visual", "Menos transparência e animações. As fontes continuam suaves." },
                    { "dicas", "Sem dicas, sugestões e apps instalados sozinhos pelo Windows." },
                    { "limpeza", "1.284 MB liberados. Arquivos em uso foram mantidos." },
                    { "dns", "Endereços de sites e servidores serão consultados de novo." }
                };
                foreach (var et in t.Etapas) Tela.MostrarResultado(et.Icone, et.Detalhe, et.Linha, Resultado.Ok(detalhes[et.Ajuste.Id]), false);
                t.Progresso(1, false);
                t.Achar<TextBlock>("TextoUltima").Text = "Última otimização: hoje às 20:41. Pode rodar de novo quando quiser.";
                t.Achar<TextBlock>("TextoBotaoOtimizar").Text = "OTIMIZAR DE NOVO";
                t.Achar<TextBlock>("TextoStatus").Text = "Backup ativo: 21 configurações podem ser desfeitas a qualquer momento.";
                t.Achar<Ellipse>("PontoStatus").Fill = new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80));
                Tela.MarcarOpcional(t.Achar<Button>("BotaoMouse"), true);
                await Task.Delay(300);
                Salvar(t, System.IO.Path.Combine(pasta, "app-concluido.png"));

                var contagem = new Dictionary<Status, int> { { Status.Ok, 10 }, { Status.Aviso, 0 }, { Status.Erro, 0 } };
                var ignorar = t.Dialogo("Otimização concluída", t.CorpoResultado(t.NumeroGrande("1.284"), contagem),
                    new OpcaoDialogo("Depois", "Botao", 0), new OpcaoDialogo("Reiniciar agora", "BotaoPrimario", 1));
                await Task.Delay(600);
                Salvar(t, System.IO.Path.Combine(pasta, "app-resultado.png"));
                t.Achar<Grid>("Sobreposicao").Visibility = Visibility.Collapsed;

                // Personalizada: perfil Máximo marcado, alguns ajustes já ativos.
                t.MostrarPagina("Personalizada", false);
                t.MarcarPerfil("maximo");
                foreach (var l in t.Linhas.Where(l => new[] { "plano", "usb", "modojogo", "gravacao", "prioridade" }.Contains(l.Ajuste.Id)))
                {
                    l.Chip.Visibility = Visibility.Visible;
                    l.BotaoDesfazer.Visibility = Visibility.Visible;
                }
                // Exemplo de algo que o Windows já tinha assim antes do FRAMEUP.
                var jaEstava = t.Linhas.First(l => l.Ajuste.Id == "dicas");
                Tela.PintarChip(jaEstava, false);
                jaEstava.Chip.Visibility = Visibility.Visible;
                await Task.Delay(400);
                Salvar(t, System.IO.Path.Combine(pasta, "app-personalizada.png"));

                // Diagnóstico com um exemplo típico.
                t.MostrarPagina("Diagnostico", false);
                var exemplo = new List<ItemDiagnostico>
                {
                    new ItemDiagnostico("Monitor 1: 60 Hz", Gravidade.Problema, "1920×1080 a 60 Hz, mas ele aceita até 144 Hz. Você está jogando com menos quadros do que o monitor mostra.", "monitor:144", "Corrigir"),
                    new ItemDiagnostico("Memória RAM abaixo da velocidade", Gravidade.Aviso, "16 GB a 2133 MHz, mas os pentes suportam 3200 MHz. Dá para ativar o XMP/DOCP na BIOS.", "ram", "Como fazer"),
                    new ItemDiagnostico("Driver de vídeo antigo", Gravidade.Aviso, "Driver de 03/2024 (2 anos). Drivers novos corrigem bugs e melhoram jogos recentes.", "url:x", "Baixar driver"),
                    new ItemDiagnostico("Disco do Windows", Gravidade.Ok, "C: 182 GB livres de 465 GB (39%)."),
                    new ItemDiagnostico("Plano de energia", Gravidade.Ok, "Plano de alto desempenho ativo."),
                    new ItemDiagnostico("Modo de Jogo", Gravidade.Ok, "Ligado."),
                    new ItemDiagnostico("Gravação em segundo plano", Gravidade.Ok, "Desligada."),
                    new ItemDiagnostico("Programas na inicialização", Gravidade.Ok, "7 programas abrem com o Windows.", "inicializacao", "Ver lista")
                };
                await t.MostrarDiagnostico(exemplo, false);
                await Task.Delay(400);
                Salvar(t, System.IO.Path.Combine(pasta, "app-diagnostico.png"));
                j.Close();
            };
        }

        static void Salvar(Tela t, string arquivo)
        {
            var raiz = t.Achar<Grid>("Raiz");
            raiz.UpdateLayout();
            int w = (int)raiz.ActualWidth, h = (int)raiz.ActualHeight;
            var rtb = new RenderTargetBitmap(w * 2, h * 2, 192, 192, PixelFormats.Pbgra32);
            rtb.Render(raiz);
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(rtb));
            using (var f = File.Create(arquivo)) png.Save(f);
        }
    }
}
#endif
