// FRAMEUP - janela, abas, botões e animações. A lógica fica em Otimizador.cs,
// a lista de ajustes em Catalogo.cs e o diagnóstico em Diagnostico.cs.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

[assembly: AssemblyTitle("FRAMEUP - Otimização gamer segura")]
[assembly: AssemblyDescription("Otimização gamer segura e reversível para Windows 10 e 11")]
[assembly: AssemblyProduct("FRAMEUP")]
[assembly: AssemblyCompany("FRAMEUP")]
[assembly: AssemblyCopyright("FRAMEUP")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

namespace Frameup
{
    public static class Programa
    {
        [STAThread]
        public static int Main(string[] args)
        {
            // Uma cópia aberta por vez: duas mexendo no mesmo backup se atrapalhariam.
            bool primeira;
#if CAPTURA
            // A versão de captura só tira prints: pode rodar com o FRAMEUP aberto.
            using (var instancia = new System.Threading.Mutex(true, @"Local\FRAMEUP-captura", out primeira))
#else
            using (var instancia = new System.Threading.Mutex(true, @"Global\FRAMEUP-otimizador", out primeira))
#endif
            {
                if (!primeira)
                {
                    MessageBox.Show("O FRAMEUP já está aberto.", "FRAMEUP", MessageBoxButton.OK, MessageBoxImage.Information);
                    return 0;
                }
                var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                var tela = new Tela(args);
                app.DispatcherUnhandledException += (s, e) => { e.Handled = true; tela.ErroInesperado(e.Exception); };
                return app.Run(tela.Janela);
            }
        }
    }

    class Etapa
    {
        public Ajuste Ajuste;
        public Border Linha;
        public Grid Icone;
        public TextBlock Detalhe;
    }

    class LinhaAjuste
    {
        public Ajuste Ajuste;
        public CheckBox Caixa;
        public Border Fundo;
        public TextBlock Descricao;
        public Grid Icone;
        public Border Chip;
        public TextBlock ChipTexto;
        public Path ChipIcone;
        public Button BotaoDesfazer;
    }

    class OpcaoDialogo
    {
        public string Texto;
        public string Estilo;
        public int Valor;
        public OpcaoDialogo(string texto, string estilo, int valor) { Texto = texto; Estilo = estilo; Valor = valor; }
    }

    public class Tela
    {
        public readonly Window Janela;
        readonly Otimizador motor;
        readonly CultureInfo br = new CultureInfo("pt-BR");
        readonly List<Ajuste> catalogo;
        readonly List<PerfilPronto> perfis;
        readonly List<Etapa> etapas = new List<Etapa>();
        readonly List<LinhaAjuste> linhas = new List<LinhaAjuste>();
        readonly Dictionary<string, Button> botoesPerfil = new Dictionary<string, Button>();
        long bytesLiberados;
        bool ocupado;
        bool diagnosticoFeito;
        bool marcandoPerfil;
        string pagina = "Inicio";

        static readonly Color Verde = Cor("#4ADE80");
        static readonly Color Amarelo = Cor("#FACC15");
        static readonly Color Vermelho = Cor("#F87171");
        static readonly Color Ciano = Cor("#22D3EE");
        static readonly Color Magenta = Cor("#E879F9");

        public Tela(string[] args)
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("FRAMEUP.Janela.xaml"))
                Janela = (Window)XamlReader.Load(s);

#if CAPTURA
            motor = new Otimizador(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "frameup-captura"));
#else
            motor = new Otimizador();
#endif
            catalogo = Catalogo.Criar(motor, b => bytesLiberados += b);
            perfis = Catalogo.Perfis(catalogo);
            MontarEtapas();
            MontarPersonalizada();
            Ligar();
            MarcarPerfil("recomendado");

#if CAPTURA
            Captura.Preparar(this, args);
#else
            Janela.Loaded += async (s, e) => await AoAbrir();
#endif
        }

        // ------------------------------------------------------------ utilidades

        static Color Cor(string hex) { return (Color)ColorConverter.ConvertFromString(hex); }
        static SolidColorBrush Pincel(string hex) { return new SolidColorBrush(Cor(hex)); }
        internal T Achar<T>(string nome) where T : class { return Janela.FindName(nome) as T; }
        string Mb(long bytes) { return (bytes / 1048576.0).ToString("N0", br); }
        Ajuste A(string id) { return catalogo.First(a => a.Id == id); }
        internal List<Etapa> Etapas { get { return etapas; } }
        internal List<LinhaAjuste> Linhas { get { return linhas; } }

        void Ligar()
        {
            Achar<Button>("BotaoFechar").Click += (s, e) => Janela.Close();
            Achar<Button>("BotaoMinimizar").Click += (s, e) => Janela.WindowState = WindowState.Minimized;
            Achar<Button>("AbaInicio").Click += (s, e) => MostrarPagina("Inicio", true);
            Achar<Button>("AbaPersonalizada").Click += (s, e) => MostrarPagina("Personalizada", true);
            Achar<Button>("AbaDiagnostico").Click += (s, e) => MostrarPagina("Diagnostico", true);
            Achar<Button>("BotaoVerMais").Click += (s, e) => MostrarPagina("Personalizada", true);
            Achar<Button>("BotaoLimparSelecao").Click += (s, e) => MarcarPerfil(null);
#if !CAPTURA
            Achar<Button>("BotaoOtimizar").Click += async (s, e) => await OtimizarTudo();
            Achar<Button>("BotaoMouse").Click += async (s, e) => await AplicarUm("mouse", Achar<Button>("BotaoMouse"));
            Achar<Button>("BotaoApps").Click += async (s, e) => await AplicarUm("appsfundo", Achar<Button>("BotaoApps"));
            Achar<Button>("BotaoInicializacao").Click += async (s, e) => await MostrarInicializacao();
            Achar<Button>("BotaoLimpeza").Click += async (s, e) => await LimpezaRapida();
            Achar<Button>("BotaoPonto").Click += async (s, e) => await PontoRestauracao();
            Achar<Button>("BotaoDesfazer").Click += async (s, e) => await Desfazer();
            Achar<Button>("BotaoDesfazer2").Click += async (s, e) => await Desfazer();
            Achar<Button>("BotaoAplicarSelecionados").Click += async (s, e) => await AplicarSelecionados();
            Achar<Button>("BotaoAnalisar").Click += async (s, e) => await Analisar();
            Janela.Closing += (s, e) => { if (ocupado) e.Cancel = true; };
#endif
            Achar<TextBlock>("TextoVersao").Text = "v" + Otimizador.Versao;
        }

        // ------------------------------------------------------------ abas

        internal void MostrarPagina(string nome, bool animar)
        {
            if (nome == pagina && animar) return;
            pagina = nome;
            foreach (var p in new[] { "Inicio", "Personalizada", "Diagnostico" })
            {
                bool ativa = p == nome;
                var conteudo = Achar<Grid>("Pagina" + p);
                var aba = Achar<Button>("Aba" + p);
                var linha = Achar<Border>("LinhaAba" + p);
                conteudo.Visibility = ativa ? Visibility.Visible : Visibility.Collapsed;
                if (ativa) aba.Foreground = Pincel("#E6EDF7"); else aba.ClearValue(Control.ForegroundProperty);
                linha.Opacity = ativa ? 1 : 0;
                if (ativa && animar)
                {
                    var escala = (ScaleTransform)linha.RenderTransform;
                    var cresce = new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                    escala.BeginAnimation(ScaleTransform.ScaleXProperty, cresce);
                    conteudo.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
                    ((TranslateTransform)conteudo.RenderTransform).BeginAnimation(TranslateTransform.YProperty,
                        new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(260)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                }
            }
#if !CAPTURA
            if (nome == "Diagnostico" && !diagnosticoFeito && !ocupado) { var t = Analisar(); }
#endif
        }

        // ------------------------------------------------------------ tela inicial

        void MontarEtapas()
        {
            foreach (var id in Catalogo.Inicio)
            {
                var a = A(id);
                var e = new Etapa { Ajuste = a };
                var grade = new Grid();
                grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
                grade.ColumnDefinitions.Add(new ColumnDefinition());
                e.Icone = NovoIcone();
                grade.Children.Add(e.Icone);
                var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                textos.Children.Add(new TextBlock { Text = a.Nome, FontWeight = FontWeights.SemiBold, FontSize = 15 });
                e.Detalhe = new TextBlock { Text = a.Descricao, FontSize = 12.5, Foreground = Pincel("#8B98AD"), TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 1, 0, 0) };
                textos.Children.Add(e.Detalhe);
                Grid.SetColumn(textos, 1);
                grade.Children.Add(textos);
                e.Linha = new Border { Child = grade, Padding = new Thickness(10, 6, 10, 6), CornerRadius = new CornerRadius(10), Background = new SolidColorBrush(Colors.Transparent) };
                Achar<StackPanel>("ListaEtapas").Children.Add(e.Linha);
                DefinirIcone(e.Icone, null);
                etapas.Add(e);
            }
        }

        static Grid NovoIcone()
        {
            return new Grid { Width = 24, Height = 24, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
        }

        // ------------------------------------------------------------ aba Personalizada

        void MontarPersonalizada()
        {
            var lista = Achar<StackPanel>("ListaPersonalizada");
            string categoria = null;
            foreach (var a in catalogo)
            {
                if (a.Categoria != categoria)
                {
                    categoria = a.Categoria;
                    lista.Children.Add(new TextBlock
                    {
                        Text = categoria.ToUpper(br), FontFamily = new FontFamily("Bahnschrift SemiBold, Segoe UI Semibold"), FontSize = 12,
                        Foreground = Pincel("#5B6779"), Margin = new Thickness(12, lista.Children.Count == 0 ? 4 : 16, 0, 4)
                    });
                }
                lista.Children.Add(NovaLinha(a));
            }

            var areaPerfis = Achar<StackPanel>("ListaPerfis");
            foreach (var p in perfis)
            {
                var conteudo = new StackPanel();
                conteudo.Children.Add(new TextBlock { Text = p.Nome, FontSize = 15, FontWeight = FontWeights.SemiBold });
                conteudo.Children.Add(new TextBlock { Text = p.Descricao, FontSize = 12.5, FontWeight = FontWeights.Normal, Foreground = Pincel("#8B98AD"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
                var b = new Button { Content = conteudo, Style = (Style)Janela.FindResource("Botao"), HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(14, 10, 14, 10), Margin = new Thickness(0, 0, 0, 8) };
                string id = p.Id;
                b.Click += (s, e) => MarcarPerfil(id);
                botoesPerfil[id] = b;
                areaPerfis.Children.Add(b);
            }
            Achar<TextBlock>("TextoSelecionadosDe").Text = " de " + catalogo.Count;
        }

        UIElement NovaLinha(Ajuste a)
        {
            var l = new LinhaAjuste { Ajuste = a };
            var grade = new Grid();
            grade.ColumnDefinitions.Add(new ColumnDefinition());
            grade.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var titulo = new StackPanel { Orientation = Orientation.Horizontal };
            titulo.Children.Add(new TextBlock { Text = a.Nome, FontWeight = FontWeights.SemiBold, FontSize = 15, VerticalAlignment = VerticalAlignment.Center });
            titulo.Children.Add(Etiqueta(a));
            textos.Children.Add(titulo);
            l.Descricao = new TextBlock { Text = a.Descricao, FontSize = 12.5, Foreground = Pincel("#8B98AD"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
            textos.Children.Add(l.Descricao);
            grade.Children.Add(textos);

            var lado = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            var chip = new StackPanel { Orientation = Orientation.Horizontal };
            l.ChipIcone = new Path { Data = Geometry.Parse("M0,4.5 L3,7.5 L9,1.5"), StrokeThickness = 1.8, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
            l.ChipTexto = new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold };
            chip.Children.Add(l.ChipIcone);
            chip.Children.Add(l.ChipTexto);
            l.Chip = new Border
            {
                Child = chip, CornerRadius = new CornerRadius(8), Padding = new Thickness(9, 4, 10, 4), Visibility = Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Center, BorderThickness = new Thickness(1)
            };
            ToolTipService.SetInitialShowDelay(l.Chip, 150);
            PintarChip(l, true);
            lado.Children.Add(l.Chip);
            l.BotaoDesfazer = new Button { Content = "Desfazer", Style = (Style)Janela.FindResource("BotaoLink"), Margin = new Thickness(6, 0, 0, 0), Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Center };
            l.BotaoDesfazer.Click += async (s, e) => { e.Handled = true; await DesfazerItem(l); };
            lado.Children.Add(l.BotaoDesfazer);
            l.Icone = NovoIcone();
            l.Icone.Margin = new Thickness(10, 0, 0, 0);
            l.Icone.Visibility = Visibility.Collapsed;
            lado.Children.Add(l.Icone);
            Grid.SetColumn(lado, 1);
            grade.Children.Add(lado);

            l.Fundo = new Border { Child = grade, CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(Colors.Transparent) };
            l.Caixa = new CheckBox { Content = l.Fundo, Style = (Style)Janela.FindResource("Caixa"), Margin = new Thickness(0, 1, 0, 1) };
            l.Caixa.Checked += (s, e) => { PularCaixa(l.Caixa); AoMarcar(); };
            l.Caixa.Unchecked += (s, e) => AoMarcar();
            linhas.Add(l);
            return l.Caixa;
        }

        // Verde "Ativo" = o FRAMEUP aplicou (e dá para desfazer).
        // Cinza "Já estava ativo" = o Windows já estava assim antes; não há o que desfazer.
        internal static void PintarChip(LinhaAjuste l, bool peloFrameup)
        {
            Color cor = peloFrameup ? Verde : Cor("#8B98AD");
            l.ChipIcone.Stroke = new SolidColorBrush(cor);
            l.ChipTexto.Foreground = new SolidColorBrush(cor);
            l.ChipTexto.Text = peloFrameup ? "Ativo" : "Já estava ativo";
            l.Chip.Background = new SolidColorBrush(Color.FromArgb(0x1C, cor.R, cor.G, cor.B));
            l.Chip.BorderBrush = new SolidColorBrush(Color.FromArgb(0x50, cor.R, cor.G, cor.B));
            l.Chip.ToolTip = peloFrameup
                ? "Aplicado pelo FRAMEUP. Clique em Desfazer para voltar ao valor de antes."
                : "Seu Windows já estava assim antes do FRAMEUP. Como nada foi mudado, não há o que desfazer.";
        }

        // Pulinho da caixa ao marcar. Só com a tela já montada (o perfil inicial marca antes).
        static void PularCaixa(CheckBox c)
        {
            if (!c.IsLoaded || c.Template == null) return;
            var caixa = c.Template.FindName("caixa", c) as Border;
            var escala = caixa == null ? null : caixa.RenderTransform as ScaleTransform;
            if (escala == null || escala.IsFrozen) return;
            var anim = new DoubleAnimationUsingKeyFrames();
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(0.7, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(1.15, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))));
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240))));
            escala.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            escala.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
        }

        UIElement Etiqueta(Ajuste a)
        {
            string texto; Color cor;
            if (a.ApagaDeVez) { texto = "APAGA DE VEZ"; cor = Amarelo; }
            else if (a.Nivel == Nivel.Recomendado) { texto = "RECOMENDADO"; cor = Ciano; }
            else if (a.Nivel == Nivel.Avancado) { texto = "AVANÇADO"; cor = Magenta; }
            else { texto = "OPCIONAL"; cor = Cor("#8B98AD"); }
            return new Border
            {
                Child = new TextBlock { Text = texto, FontFamily = new FontFamily("Bahnschrift SemiBold, Segoe UI Semibold"), FontSize = 10, Foreground = new SolidColorBrush(cor) },
                Background = new SolidColorBrush(Color.FromArgb(0x1C, cor.R, cor.G, cor.B)), CornerRadius = new CornerRadius(5),
                Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(10, 1, 0, 0), VerticalAlignment = VerticalAlignment.Center
            };
        }

        internal void MarcarPerfil(string id)
        {
            var perfil = perfis.FirstOrDefault(p => p.Id == id);
            marcandoPerfil = true;
            foreach (var l in linhas) l.Caixa.IsChecked = perfil != null && perfil.Ajustes.Contains(l.Ajuste.Id);
            marcandoPerfil = false;
            DestacarPerfil(id);
            AoMarcar();
        }

        void DestacarPerfil(string id)
        {
            foreach (var par in botoesPerfil)
            {
                if (par.Key == id)
                {
                    par.Value.Background = new SolidColorBrush(Color.FromArgb(0x1A, Ciano.R, Ciano.G, Ciano.B));
                    par.Value.BorderBrush = new SolidColorBrush(Color.FromArgb(0x90, Ciano.R, Ciano.G, Ciano.B));
                }
                else
                {
                    par.Value.ClearValue(Control.BackgroundProperty);
                    par.Value.ClearValue(Control.BorderBrushProperty);
                }
            }
        }

        void AoMarcar()
        {
            if (marcandoPerfil) return;
            var marcados = new HashSet<string>(linhas.Where(l => l.Caixa.IsChecked == true).Select(l => l.Ajuste.Id));
            var igual = perfis.FirstOrDefault(p => p.Ajustes.SetEquals(marcados));
            DestacarPerfil(igual == null ? null : igual.Id);

            var sel = linhas.Where(l => l.Caixa.IsChecked == true).Select(l => l.Ajuste).ToList();
            Achar<TextBlock>("TextoSelecionados").Text = sel.Count.ToString(br);
            var partes = new List<string>();
            int rec = sel.Count(a => a.Nivel == Nivel.Recomendado), ava = sel.Count(a => a.Nivel == Nivel.Avancado), opc = sel.Count(a => a.Nivel == Nivel.Opcional);
            if (rec > 0) partes.Add(rec + (rec == 1 ? " recomendado" : " recomendados"));
            if (ava > 0) partes.Add(ava + (ava == 1 ? " avançado" : " avançados"));
            if (opc > 0) partes.Add(opc + (opc == 1 ? " opcional" : " opcionais"));
            Achar<TextBlock>("TextoSelecionadosDetalhe").Text = partes.Count == 0 ? "Marque pelo menos um ajuste." : string.Join("  ·  ", partes);
            Achar<Button>("BotaoAplicarSelecionados").IsEnabled = sel.Count > 0 && !ocupado;
        }

        // ------------------------------------------------------------ ícones e animações

        internal static void DefinirIcone(Grid icone, Status? status)
        {
            icone.Children.Clear();
            icone.RenderTransform = null;
            if (status == null)
            {
                icone.Children.Add(new Ellipse { Stroke = Pincel("#2A3447"), StrokeThickness = 2 });
                return;
            }
            Color cor = status == Status.Ok ? Verde : status == Status.Aviso ? Amarelo : Vermelho;
            icone.Children.Add(new Ellipse { Fill = new SolidColorBrush(cor) });
            var tinta = Pincel("#06121A");
            if (status == Status.Ok)
                icone.Children.Add(new Path { Data = Geometry.Parse("M7,12.5 L10.5,16 L17,8.5"), Stroke = tinta, StrokeThickness = 2.4, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round });
            else if (status == Status.Aviso)
                icone.Children.Add(new TextBlock { Text = "!", Foreground = tinta, FontWeight = FontWeights.Black, FontSize = 15, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            else
                icone.Children.Add(new Path { Data = Geometry.Parse("M8.5,8.5 L15.5,15.5 M15.5,8.5 L8.5,15.5"), Stroke = tinta, StrokeThickness = 2.4, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
        }

        static void Girando(Grid icone, TextBlock detalhe)
        {
            icone.Children.Clear();
            icone.RenderTransform = null;
            icone.Children.Add(new Ellipse { Stroke = Pincel("#1F3A48"), StrokeThickness = 2.5 });
            var arco = new Path
            {
                Data = Geometry.Parse("M12,1.25 A10.75,10.75 0 0 1 22.75,12"),
                Stroke = new SolidColorBrush(Ciano), StrokeThickness = 2.5,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
            };
            var giro = new RotateTransform(0, 12, 12);
            arco.RenderTransform = giro;
            giro.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(750)) { RepeatBehavior = RepeatBehavior.Forever });
            icone.Children.Add(arco);
            if (detalhe != null)
            {
                detalhe.Foreground = new SolidColorBrush(Ciano);
                detalhe.Text = "aplicando…";
            }
        }

        static void Pop(UIElement alvo)
        {
            var escala = new ScaleTransform(0.3, 0.3);
            alvo.RenderTransformOrigin = new Point(0.5, 0.5);
            alvo.RenderTransform = escala;
            var anim = new DoubleAnimationUsingKeyFrames();
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(1.2, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(170)), new CubicEase { EasingMode = EasingMode.EaseOut }));
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300)), new CubicEase { EasingMode = EasingMode.EaseInOut }));
            escala.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            escala.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
        }

        static void Brilhar(Border linha, Color cor)
        {
            var fundo = new SolidColorBrush(Color.FromArgb(0x26, cor.R, cor.G, cor.B));
            linha.Background = fundo;
            fundo.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(Color.FromArgb(0, cor.R, cor.G, cor.B), TimeSpan.FromMilliseconds(1100)) { BeginTime = TimeSpan.FromMilliseconds(150) });
        }

        static Color CorDo(Status s) { return s == Status.Ok ? Verde : s == Status.Aviso ? Amarelo : Vermelho; }

        internal static void MostrarResultado(Grid icone, TextBlock detalhe, Border linha, Resultado r, bool animar)
        {
            DefinirIcone(icone, r.Status);
            detalhe.Text = r.Detalhe;
            detalhe.Foreground = Pincel(r.Status == Status.Ok ? "#8B98AD" : r.Status == Status.Aviso ? "#E9C46A" : "#F87171");
            if (animar)
            {
                Pop(icone);
                Brilhar(linha, CorDo(r.Status));
            }
        }

        internal void Progresso(double fracao, bool animar)
        {
            var barra = Achar<Border>("BarraProgresso");
            double alvo = Achar<Border>("TrilhaProgresso").ActualWidth * fracao;
            if (!animar) { barra.BeginAnimation(FrameworkElement.WidthProperty, null); barra.Width = alvo; return; }
            barra.BeginAnimation(FrameworkElement.WidthProperty,
                new DoubleAnimation(alvo, TimeSpan.FromMilliseconds(420)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }

        // ------------------------------------------------------------ diálogos e avisos

        TaskCompletionSource<int> dialogoAtual;

        // Erros não previstos viram um aviso na tela (e no log) em vez de fechar o programa.
        internal void ErroInesperado(Exception ex)
        {
            motor.Log("Erro inesperado: " + ex);
            FecharDialogo(0);
            Ocupado(false);
            Toast("Algo deu errado: " + ex.Message, Vermelho);
        }

        internal Task<int> Dialogo(string titulo, UIElement corpo, params OpcaoDialogo[] opcoes)
        {
            if (dialogoAtual != null) dialogoAtual.TrySetResult(0);
            dialogoAtual = new TaskCompletionSource<int>();
            Achar<TextBlock>("DialogoTitulo").Text = titulo;
            var areaCorpo = Achar<StackPanel>("DialogoCorpo");
            areaCorpo.Children.Clear();
            if (corpo != null) areaCorpo.Children.Add(corpo);

            var botoes = Achar<StackPanel>("DialogoBotoes");
            botoes.Children.Clear();
            foreach (var o in opcoes)
            {
                var b = new Button { Content = o.Texto, Style = (Style)Janela.FindResource(o.Estilo), Margin = new Thickness(10, 0, 0, 0), MinWidth = 120 };
                if (o.Estilo == "BotaoPrimario") { b.Height = 46; b.FontSize = 15; b.Padding = new Thickness(22, 0, 22, 0); }
                if (o.Estilo == "BotaoAviso") { b.Height = 46; b.Padding = new Thickness(22, 0, 22, 0); }
                int valor = o.Valor;
                b.Click += (s, e) => FecharDialogo(valor);
                botoes.Children.Add(b);
            }

            var sobre = Achar<Grid>("Sobreposicao");
            sobre.Visibility = Visibility.Visible;
            sobre.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
            var escala = (ScaleTransform)Achar<Border>("CartaoDialogo").RenderTransform;
            var entrada = new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut } };
            escala.BeginAnimation(ScaleTransform.ScaleXProperty, entrada);
            escala.BeginAnimation(ScaleTransform.ScaleYProperty, entrada);
            return dialogoAtual.Task;
        }

        void FecharDialogo(int valor)
        {
            Achar<Grid>("Sobreposicao").Visibility = Visibility.Collapsed;
            var t = dialogoAtual;
            dialogoAtual = null;
            if (t != null) t.TrySetResult(valor);
        }

        internal static TextBlock Paragrafo(string texto, string cor = "#C9D3E3")
        {
            return new TextBlock { Text = texto, TextWrapping = TextWrapping.Wrap, Foreground = Pincel(cor), FontSize = 14.5, LineHeight = 22, Margin = new Thickness(0, 0, 0, 8) };
        }

        internal static StackPanel Lista(IEnumerable<string> itens, string corMarcador = "#E879F9")
        {
            var p = new StackPanel { Margin = new Thickness(0, 2, 0, 6) };
            foreach (var i in itens)
            {
                var linha = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
                linha.Children.Add(new Ellipse { Width = 6, Height = 6, Fill = Pincel(corMarcador), Margin = new Thickness(2, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center });
                linha.Children.Add(new TextBlock { Text = i, TextWrapping = TextWrapping.Wrap, FontSize = 14 });
                p.Children.Add(linha);
            }
            return p;
        }

        internal static UIElement Rolavel(UIElement conteudo, double altura)
        {
            return new ScrollViewer { Content = conteudo, MaxHeight = altura, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        }

        internal async void Toast(string texto, Color cor)
        {
            var area = Achar<StackPanel>("AreaToast");
            var linha = new StackPanel { Orientation = Orientation.Horizontal };
            linha.Children.Add(new Ellipse { Width = 9, Height = 9, Fill = new SolidColorBrush(cor), Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center });
            linha.Children.Add(new TextBlock { Text = texto, FontSize = 14, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 420, TextWrapping = TextWrapping.Wrap });
            var cartao = new Border
            {
                Child = linha, Background = Pincel("#182133"), BorderBrush = Pincel("#2A3650"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12), Padding = new Thickness(18, 13, 20, 13), Margin = new Thickness(0, 8, 0, 0),
                RenderTransform = new TranslateTransform(40, 0), Opacity = 0,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 24, ShadowDepth = 0, Opacity = 0.5, Color = Colors.Black }
            };
            area.Children.Add(cartao);
            var facil = new CubicEase { EasingMode = EasingMode.EaseOut };
            cartao.RenderTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(260)) { EasingFunction = facil });
            cartao.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(200)));
            await Task.Delay(3600);
            cartao.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(300)));
            await Task.Delay(320);
            area.Children.Remove(cartao);
        }

        async Task ContarAte(TextBlock alvo, long bytes)
        {
            double final = bytes / 1048576.0;
            const int quadros = 36;
            for (int i = 1; i <= quadros; i++)
            {
                double t = 1 - Math.Pow(1 - i / (double)quadros, 3);
                alvo.Text = (final * t).ToString("N0", br);
                await Task.Delay(16);
            }
        }

        internal UIElement CorpoResultado(TextBlock numero, Dictionary<Status, int> contagem)
        {
            return Juntar(
                Rotulo("ESPAÇO LIBERADO"),
                Linha(numero, new TextBlock { Text = " MB", FontSize = 22, FontWeight = FontWeights.SemiBold, Foreground = Pincel("#8B98AD"), VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(6, 0, 0, 10) }),
                Paragrafo(contagem[Status.Ok] + " concluídos  ·  " + contagem[Status.Aviso] + " avisos  ·  " + contagem[Status.Erro] + " erros", "#8B98AD"),
                Paragrafo("Reinicie o PC para aplicar 100%. Cada ajuste pode ser desfeito na aba Personalizada, ou tudo de uma vez em DESFAZER TUDO."));
        }

        internal TextBlock NumeroGrande(string texto)
        {
            return new TextBlock { FontFamily = new FontFamily("Bahnschrift, Segoe UI"), FontWeight = FontWeights.Bold, FontSize = 52, Foreground = (Brush)Janela.FindResource("Grad"), Text = texto };
        }

        // ------------------------------------------------------------ estado da tela

        async Task AoAbrir()
        {
            var info = await Task.Run(() => Otimizador.LerInfoPC());
            Achar<TextBlock>("InfoCpu").Text = info.Cpu;
            Achar<TextBlock>("InfoGpu").Text = info.Gpu;
            Achar<TextBlock>("InfoRam").Text = info.Ram;
            Achar<TextBlock>("InfoSo").Text = info.Windows;
            Achar<TextBlock>("InfoCpu").ToolTip = info.Cpu;
            Achar<TextBlock>("InfoGpu").ToolTip = info.Gpu;
            AtualizarEstado();

            if (motor.BackupInvalido)
            {
                await Dialogo("Backup danificado",
                    Paragrafo("O arquivo de backup do FRAMEUP está danificado. Para proteger seu PC, as otimizações ficam bloqueadas.\n\nSe quiser voltar ao estado anterior, use a Restauração do Sistema do Windows (ponto \"FRAMEUP - antes da otimizacao\").\n\nArquivo: " + motor.ArquivoBackup),
                    new OpcaoDialogo("Entendi", "Botao", 0));
            }
        }

        static bool Tentar(Func<bool> f)
        {
            if (f == null) return false;
            try { return f(); } catch { return false; }
        }

        async void AtualizarEstado()
        {
            var b = motor.Backup;
            var status = Achar<TextBlock>("TextoStatus");
            var ponto = Achar<Ellipse>("PontoStatus");
            if (motor.BackupInvalido)
            {
                status.Text = "Backup danificado: alterações bloqueadas para proteger seu PC.";
                ponto.Fill = new SolidColorBrush(Amarelo);
            }
            else if (b.TemAlteracoes())
            {
                status.Text = "Backup ativo: " + b.Registro.Count + " configurações podem ser desfeitas a qualquer momento.";
                ponto.Fill = new SolidColorBrush(Verde);
            }
            else
            {
                status.Text = "Nenhuma alteração feita neste PC ainda.";
                ponto.Fill = Pincel("#5B6779");
            }
            Achar<TextBlock>("TextoUltima").Text = string.IsNullOrEmpty(b.UltimaOtimizacao)
                ? "Faz backup de tudo antes. Pode ser desfeito a qualquer momento."
                : "Última otimização: " + b.UltimaOtimizacao + ". Pode rodar de novo quando quiser.";
            foreach (var n in new[] { "BotaoDesfazer", "BotaoDesfazer2" })
                Achar<Button>(n).IsEnabled = b.TemAlteracoes() && !ocupado;

            // Ler o estado de cada ajuste chama o powercfg: fora da tela, para não travar.
            var estados = await Task.Run(() => catalogo.ToDictionary(a => a.Id, a => new[] { Tentar(a.Ativo), Tentar(a.PodeDesfazer) }));
            foreach (var l in linhas)
            {
                bool ativo = estados[l.Ajuste.Id][0], podeDesfazer = estados[l.Ajuste.Id][1];
                PintarChip(l, podeDesfazer);
                l.Chip.Visibility = ativo ? Visibility.Visible : Visibility.Collapsed;
                l.BotaoDesfazer.Visibility = podeDesfazer && !ocupado ? Visibility.Visible : Visibility.Collapsed;
            }
            MarcarOpcional(Achar<Button>("BotaoMouse"), estados["mouse"][0], estados["mouse"][1]);
            MarcarOpcional(Achar<Button>("BotaoApps"), estados["appsfundo"][0], estados["appsfundo"][1]);
        }

        internal static void MarcarOpcional(Button botao, bool ativo, bool peloFrameup = true)
        {
            if (ativo)
            {
                Color cor = peloFrameup ? Verde : Cor("#8B98AD");
                var p = new StackPanel { Orientation = Orientation.Horizontal };
                p.Children.Add(new Path { Data = Geometry.Parse("M0,5 L3.5,8.5 L10,1.5"), Stroke = new SolidColorBrush(cor), StrokeThickness = 2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
                p.Children.Add(new TextBlock { Text = peloFrameup ? "Ativo" : "Já ativo", Foreground = new SolidColorBrush(cor) });
                botao.Content = p;
                botao.Tag = "ativo";
                botao.ToolTip = peloFrameup ? null : "Seu Windows já estava assim antes do FRAMEUP.";
                botao.Background = new SolidColorBrush(Color.FromArgb(0x18, cor.R, cor.G, cor.B));
                botao.BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, cor.R, cor.G, cor.B));
            }
            else
            {
                botao.Content = "Aplicar";
                botao.Tag = null;
                botao.ClearValue(Control.BackgroundProperty);
                botao.ClearValue(Control.BorderBrushProperty);
            }
        }

        void Ocupado(bool sim)
        {
            ocupado = sim;
            foreach (var nome in new[] { "BotaoOtimizar", "BotaoMouse", "BotaoApps", "BotaoInicializacao", "BotaoLimpeza", "BotaoPonto",
                                         "BotaoDesfazer", "BotaoDesfazer2", "BotaoAplicarSelecionados", "BotaoAnalisar", "BotaoLimparSelecao" })
                Achar<Button>(nome).IsEnabled = !sim;
            foreach (var b in botoesPerfil.Values) b.IsEnabled = !sim;
            foreach (var l in linhas)
            {
                l.Caixa.IsEnabled = !sim;
                if (sim) l.BotaoDesfazer.Visibility = Visibility.Collapsed;
            }
            if (!sim) { AoMarcar(); AtualizarEstado(); }
        }

        async Task<Resultado> Rodar(Func<Resultado> acao, string nome)
        {
            Resultado r;
            try { r = await Task.Run(acao); }
            catch (Exception ex) { r = Resultado.Erro(ex.Message); }
            motor.Log(r.Status + " | " + nome + " | " + r.Detalhe);
            return r;
        }

        // ------------------------------------------------------------ ações: tela inicial

        async Task OtimizarTudo()
        {
            if (ocupado) return;
            if (motor.BackupInvalido) { await AoAbrir(); return; }

            int escolha = await Dialogo("Otimizar este PC?",
                Juntar(Paragrafo("O FRAMEUP vai aplicar os " + etapas.Count + " ajustes recomendados:"),
                       Lista(etapas.Select(e => e.Ajuste.Nome)),
                       Paragrafo("Antes de cada ajuste, o valor original é salvo. Não mexe em antivírus, Windows Update, drivers nem serviços.", "#8B98AD")),
                new OpcaoDialogo("Cancelar", "Botao", 0), new OpcaoDialogo("Começar", "BotaoPrimario", 1));
            if (escolha != 1) return;

            Ocupado(true);
            Achar<TextBlock>("TextoBotaoOtimizar").Text = "OTIMIZANDO…";
            bytesLiberados = 0;
            foreach (var e in etapas) { DefinirIcone(e.Icone, null); e.Detalhe.Text = e.Ajuste.Descricao; e.Detalhe.Foreground = Pincel("#8B98AD"); }
            Progresso(0, false);

            var contagem = NovaContagem();
            for (int i = 0; i < etapas.Count; i++)
            {
                var e = etapas[i];
                Girando(e.Icone, e.Detalhe);
                var espera = Task.Delay(380);
                var r = await Rodar(e.Ajuste.Aplicar, e.Ajuste.Nome);
                await espera;
                MostrarResultado(e.Icone, e.Detalhe, e.Linha, r, true);
                contagem[r.Status]++;
                Progresso((i + 1) / (double)etapas.Count, true);
                await Task.Delay(90);
            }
            motor.MarcarOtimizado();
            diagnosticoFeito = false;
            Achar<TextBlock>("TextoBotaoOtimizar").Text = "OTIMIZAR DE NOVO";
            await Task.Delay(350);
            await MostrarConclusao(contagem);
            Ocupado(false);
        }

        static Dictionary<Status, int> NovaContagem()
        {
            return new Dictionary<Status, int> { { Status.Ok, 0 }, { Status.Aviso, 0 }, { Status.Erro, 0 } };
        }

        async Task MostrarConclusao(Dictionary<Status, int> contagem)
        {
            var numero = NumeroGrande("0");
            var tarefa = Dialogo(contagem[Status.Erro] == 0 ? "Otimização concluída" : "Otimização concluída com erros",
                CorpoResultado(numero, contagem), new OpcaoDialogo("Depois", "Botao", 0), new OpcaoDialogo("Reiniciar agora", "BotaoPrimario", 1));
            await ContarAte(numero, bytesLiberados);
            if (await tarefa == 1) await ConfirmarReinicio();
        }

        async Task ConfirmarReinicio()
        {
            int ok = await Dialogo("Reiniciar o PC?",
                Paragrafo("Salve o que estiver aberto. O Windows vai reiniciar em 15 segundos."),
                new OpcaoDialogo("Cancelar", "Botao", 0), new OpcaoDialogo("Reiniciar", "BotaoPrimario", 1));
            if (ok == 1)
            {
                await Task.Run(() => Otimizador.Reiniciar());
                Toast("Reiniciando em 15 segundos…", Ciano);
            }
        }

        async Task AplicarUm(string id, Button botao)
        {
            if (ocupado) return;
            var a = A(id);
            if ((botao.Tag as string) == "ativo")
            {
                Toast(Tentar(a.PodeDesfazer)
                    ? "Já está ativo. Para voltar, use Desfazer na aba Personalizada."
                    : "Seu Windows já estava assim antes do FRAMEUP. Não há o que mudar.", Ciano);
                return;
            }
            Ocupado(true);
            var r = await Rodar(a.Aplicar, a.Nome);
            Ocupado(false);
            diagnosticoFeito = false;
            if (r.Status == Status.Ok) { Pop(botao); Toast(a.Nome + ": " + r.Detalhe, Verde); }
            else await Dialogo("Não deu certo", Paragrafo(r.Detalhe), new OpcaoDialogo("OK", "Botao", 0));
        }

        async Task MostrarInicializacao()
        {
            if (ocupado) return;
            Ocupado(true);
            var lista = await Task.Run(() => Otimizador.ProgramasNaInicializacao());
            Ocupado(false);

            UIElement corpo;
            if (lista.Count == 0)
                corpo = Paragrafo("Nenhum programa está configurado para abrir junto com o Windows.");
            else
                corpo = Juntar(
                    Paragrafo(lista.Count + " programas configurados para abrir com o Windows:"),
                    Rolavel(Lista(lista, "#22D3EE"), 240),
                    Paragrafo("Desative o que você não usa enquanto joga. Antivírus e drivers de áudio/vídeo devem ficar ligados. O FRAMEUP não desativa nada sozinho.", "#8B98AD"));
            int escolha = await Dialogo("Programas na inicialização", corpo,
                new OpcaoDialogo("Fechar", "Botao", 0), new OpcaoDialogo("Abrir Gerenciador", "BotaoPrimario", 1));
            if (escolha == 1)
            {
                try { Otimizador.AbrirGerenciadorDeTarefas(); }
                catch (Exception ex) { Toast("Não abriu o Gerenciador: " + ex.Message, Vermelho); }
            }
        }

        async Task LimpezaRapida()
        {
            if (ocupado) return;
            Ocupado(true);
            bytesLiberados = 0;
            var r = await Rodar(A("limpeza").Aplicar, "Limpeza rápida");
            await Rodar(A("dns").Aplicar, "Cache de DNS");
            Ocupado(false);
            diagnosticoFeito = false;
            if (r.Status == Status.Ok) Toast(Mb(bytesLiberados) + " MB liberados", Verde);
            else Toast(r.Detalhe, Amarelo);
        }

        async Task PontoRestauracao()
        {
            if (ocupado) return;
            Ocupado(true);
            var r = await Rodar(() => motor.CriarPontoRestauracao(), "Ponto de restauração");
            Ocupado(false);
            if (r.Status == Status.Ok) { Toast("Ponto de restauração disponível.", Verde); return; }

            int ok = await Dialogo("Proteção do Sistema desligada",
                Paragrafo("O Windows não deixou criar o ponto de restauração. Quer ativar a Proteção do Sistema no disco do Windows e tentar de novo? O Windows reserva um pouco de espaço para os pontos."),
                new OpcaoDialogo("Agora não", "Botao", 0), new OpcaoDialogo("Ativar e criar", "BotaoPrimario", 1));
            if (ok != 1) return;

            Ocupado(true);
            var ativar = await Rodar(() => motor.AtivarProtecaoSistema(), "Ativar Proteção do Sistema");
            var nova = ativar.Status == Status.Ok ? await Rodar(() => motor.CriarPontoRestauracao(), "Ponto de restauração") : ativar;
            Ocupado(false);
            if (nova.Status == Status.Ok) Toast("Proteção ativada e ponto criado.", Verde);
            else await Dialogo("Não deu certo", Paragrafo(nova.Detalhe), new OpcaoDialogo("OK", "Botao", 0));
        }

        async Task Desfazer()
        {
            if (ocupado) return;
            int ok = await Dialogo("Desfazer tudo?",
                Juntar(Paragrafo("Todas as configurações alteradas pelo FRAMEUP voltam ao valor que tinham antes, e o plano de energia original é reativado."),
                       Paragrafo("Arquivos temporários apagados não voltam (o Windows recria o que precisar).", "#8B98AD")),
                new OpcaoDialogo("Cancelar", "Botao", 0), new OpcaoDialogo("Desfazer tudo", "BotaoAviso", 1));
            if (ok != 1) return;

            Ocupado(true);
            List<Resultado> resultados;
            try { resultados = await Task.Run(() => motor.DesfazerTudo()); }
            catch (Exception ex) { resultados = new List<Resultado> { Resultado.Erro(ex.Message) }; }
            diagnosticoFeito = false;

            bool erro = resultados.Any(r => r.Status == Status.Erro);
            if (!erro)
            {
                foreach (var e in etapas) { DefinirIcone(e.Icone, null); e.Detalhe.Text = e.Ajuste.Descricao; e.Detalhe.Foreground = Pincel("#8B98AD"); }
                foreach (var l in linhas) { l.Icone.Visibility = Visibility.Collapsed; l.Descricao.Text = l.Ajuste.Descricao; l.Descricao.Foreground = Pincel("#8B98AD"); }
                Progresso(0, true);
                Achar<TextBlock>("TextoBotaoOtimizar").Text = "OTIMIZAR AGORA";
            }
            var corpo = Juntar(
                Lista(resultados.Select(r => r.Detalhe), erro ? "#F87171" : "#4ADE80"),
                Paragrafo(erro ? "O backup foi mantido. Tente de novo; se continuar, use a Restauração do Sistema do Windows."
                               : "Reinicie o PC para concluir.", "#8B98AD"));
            int r2 = await Dialogo(erro ? "Desfeito em parte" : "PC de volta ao original", corpo,
                new OpcaoDialogo("Depois", "Botao", 0), new OpcaoDialogo("Reiniciar agora", "BotaoPrimario", 1));
            if (r2 == 1) await ConfirmarReinicio();
            Ocupado(false);
        }

        // ------------------------------------------------------------ ações: Personalizada

        async Task AplicarSelecionados()
        {
            if (ocupado) return;
            if (motor.BackupInvalido) { await AoAbrir(); return; }
            var sel = linhas.Where(l => l.Caixa.IsChecked == true).ToList();
            if (sel.Count == 0) { Toast("Marque pelo menos um ajuste.", Amarelo); return; }

            if (sel.Any(l => l.Ajuste.ApagaDeVez))
            {
                long tamanho = await Task.Run(() => Otimizador.TamanhoLixeira());
                string quanto = tamanho > 0 ? " (" + Mb(tamanho) + " MB)" : "";
                int lixeira = await Dialogo("Esvaziar a lixeira?",
                    Paragrafo("Os arquivos da lixeira" + quanto + " serão apagados de vez. Isso não pode ser desfeito."),
                    new OpcaoDialogo("Voltar", "Botao", 0), new OpcaoDialogo("Esvaziar e continuar", "BotaoAviso", 1));
                if (lixeira != 1) return;
            }

            int escolha = await Dialogo("Aplicar " + sel.Count + (sel.Count == 1 ? " ajuste?" : " ajustes?"),
                Juntar(Rolavel(Lista(sel.Select(l => l.Ajuste.Nome)), 250),
                       Paragrafo("Antes de cada ajuste, o valor original é salvo.", "#8B98AD")),
                new OpcaoDialogo("Cancelar", "Botao", 0), new OpcaoDialogo("Aplicar", "BotaoPrimario", 1));
            if (escolha != 1) return;

            Ocupado(true);
            bytesLiberados = 0;
            foreach (var l in linhas) { l.Icone.Visibility = Visibility.Collapsed; l.Descricao.Text = l.Ajuste.Descricao; l.Descricao.Foreground = Pincel("#8B98AD"); }
            var contagem = NovaContagem();
            foreach (var l in sel)
            {
                l.Caixa.BringIntoView();
                l.Icone.Visibility = Visibility.Visible;
                Girando(l.Icone, l.Descricao);
                var espera = Task.Delay(320);
                var r = await Rodar(l.Ajuste.Aplicar, l.Ajuste.Nome);
                await espera;
                MostrarResultado(l.Icone, l.Descricao, l.Fundo, r, true);
                contagem[r.Status]++;
                await Task.Delay(70);
            }
            motor.MarcarOtimizado();
            diagnosticoFeito = false;
            await Task.Delay(300);
            await MostrarConclusao(contagem);
            Ocupado(false);
        }

        async Task DesfazerItem(LinhaAjuste l)
        {
            if (ocupado || l.Ajuste.Desfazer == null) return;
            Ocupado(true);
            l.Icone.Visibility = Visibility.Visible;
            Girando(l.Icone, null);
            var r = await Rodar(l.Ajuste.Desfazer, "Desfazer " + l.Ajuste.Nome);
            MostrarResultado(l.Icone, l.Descricao, l.Fundo, r.Status == Status.Ok ? Resultado.Ok("Desfeito: voltou ao valor original.") : r, true);
            diagnosticoFeito = false;
            Ocupado(false);
            Toast(r.Status == Status.Ok ? l.Ajuste.Nome + " desfeito." : r.Detalhe, CorDo(r.Status));
        }

        // ------------------------------------------------------------ ações: Diagnóstico

        async Task Analisar()
        {
            if (ocupado) return;
            Ocupado(true);
            var lista = Achar<StackPanel>("ListaDiagnostico");
            lista.Children.Clear();
            var carregando = NovoIcone();
            Girando(carregando, null);
            var linhaCarregando = Linha(carregando, new TextBlock { Text = "Analisando seu PC…", Foreground = Pincel("#8B98AD"), FontSize = 14, Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            ((FrameworkElement)linhaCarregando).Margin = new Thickness(12, 10, 0, 0);
            lista.Children.Add(linhaCarregando);
            DesenharNota(0);
            Achar<TextBlock>("TextoNota").Text = "—";
            Achar<TextBlock>("TextoNotaRotulo").Text = "Analisando…";
            Achar<TextBlock>("TextoNotaRotulo").Foreground = Pincel("#E6EDF7");
            Achar<TextBlock>("TextoResumoDiag").Text = "";

            var espera = Task.Delay(600);
            List<ItemDiagnostico> itens;
            try { itens = await Task.Run(() => Diagnostico.Analisar()); }
            catch (Exception ex) { motor.Log("Diagnóstico: " + ex.Message); itens = new List<ItemDiagnostico>(); }
            await espera;
            diagnosticoFeito = true;
            await MostrarDiagnostico(itens, true);
            Ocupado(false);
        }

        internal async Task MostrarDiagnostico(List<ItemDiagnostico> itens, bool animar)
        {
            var lista = Achar<StackPanel>("ListaDiagnostico");
            lista.Children.Clear();
            var ordenados = itens.OrderBy(i => i.Gravidade == Gravidade.Problema ? 0 : i.Gravidade == Gravidade.Aviso ? 1 : 2).ToList();
            foreach (var item in ordenados)
            {
                var linha = LinhaDiagnostico(item);
                lista.Children.Add(linha);
                if (animar)
                {
                    linha.Opacity = 0;
                    linha.RenderTransform = new TranslateTransform(0, 8);
                    linha.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(220)));
                    linha.RenderTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(260)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                    await Task.Delay(55);
                }
            }

            int nota = Diagnostico.Nota(itens);
            int problemas = itens.Count(i => i.Gravidade == Gravidade.Problema), avisos = itens.Count(i => i.Gravidade == Gravidade.Aviso);
            var rotulo = Achar<TextBlock>("TextoNotaRotulo");
            rotulo.Text = Diagnostico.Rotulo(nota);
            rotulo.Foreground = new SolidColorBrush(nota >= 75 ? Verde : nota >= 50 ? Amarelo : Vermelho);
            Achar<TextBlock>("TextoResumoDiag").Text = problemas + avisos == 0
                ? "Nada para corrigir. Seu PC está em dia."
                : (problemas > 0 ? problemas + (problemas == 1 ? " problema" : " problemas") + (avisos > 0 ? " e " : "") : "")
                  + (avisos > 0 ? avisos + (avisos == 1 ? " ponto de atenção" : " pontos de atenção") : "");

            if (!animar) { DesenharNota(nota); Achar<TextBlock>("TextoNota").Text = nota.ToString(br); return; }
            const int quadros = 45;
            for (int i = 1; i <= quadros; i++)
            {
                double t = 1 - Math.Pow(1 - i / (double)quadros, 3);
                DesenharNota(nota * t);
                Achar<TextBlock>("TextoNota").Text = Math.Round(nota * t).ToString(br);
                await Task.Delay(16);
            }
        }

        // Arco do medidor: começa no topo e anda no sentido horário até a nota.
        internal void DesenharNota(double nota)
        {
            var arco = Achar<Path>("ArcoNota");
            if (nota <= 0.5) { arco.Data = null; return; }
            const double centro = 110, raio = 102;
            double angulo = Math.Min(nota, 99.9) / 100 * 360;
            double rad = (angulo - 90) * Math.PI / 180;
            var figura = new PathFigure { StartPoint = new Point(centro, centro - raio) };
            figura.Segments.Add(new ArcSegment(new Point(centro + raio * Math.Cos(rad), centro + raio * Math.Sin(rad)),
                new Size(raio, raio), 0, angulo > 180, SweepDirection.Clockwise, true));
            var g = new PathGeometry();
            g.Figures.Add(figura);
            arco.Data = g;
        }

        Border LinhaDiagnostico(ItemDiagnostico item)
        {
            var grade = new Grid();
            grade.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            grade.ColumnDefinitions.Add(new ColumnDefinition());
            grade.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icone = NovoIcone();
            DefinirIcone(icone, item.Gravidade == Gravidade.Ok ? Status.Ok : item.Gravidade == Gravidade.Aviso ? Status.Aviso : Status.Erro);
            if (item.Gravidade == Gravidade.Problema)
            {
                icone.Children.Clear();
                icone.Children.Add(new Ellipse { Fill = new SolidColorBrush(Vermelho) });
                icone.Children.Add(new TextBlock { Text = "!", Foreground = Pincel("#06121A"), FontWeight = FontWeights.Black, FontSize = 15, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            }
            grade.Children.Add(icone);

            var textos = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            textos.Children.Add(new TextBlock { Text = item.Titulo, FontWeight = FontWeights.SemiBold, FontSize = 15 });
            textos.Children.Add(new TextBlock { Text = item.Detalhe, FontSize = 12.5, Foreground = Pincel("#8B98AD"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetColumn(textos, 1);
            grade.Children.Add(textos);

            if (item.AcaoTexto != null)
            {
                var b = new Button
                {
                    Content = item.AcaoTexto, Style = (Style)Janela.FindResource(item.Gravidade == Gravidade.Ok ? "BotaoLink" : "Botao"),
                    Margin = new Thickness(14, 0, 0, 0), Padding = new Thickness(14, 8, 14, 8), FontSize = 13, VerticalAlignment = VerticalAlignment.Center
                };
                string acao = item.Acao;
                b.Click += async (s, e) => await AcaoDiagnostico(acao);
                Grid.SetColumn(b, 2);
                grade.Children.Add(b);
            }

            Color cor = item.Gravidade == Gravidade.Problema ? Vermelho : item.Gravidade == Gravidade.Aviso ? Amarelo : Colors.Transparent;
            return new Border
            {
                Child = grade, CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 11, 12, 11), Margin = new Thickness(0, 0, 0, 6),
                Background = new SolidColorBrush(item.Gravidade == Gravidade.Ok ? Colors.Transparent : Color.FromArgb(0x10, cor.R, cor.G, cor.B)),
                BorderBrush = new SolidColorBrush(item.Gravidade == Gravidade.Ok ? Colors.Transparent : Color.FromArgb(0x38, cor.R, cor.G, cor.B)),
                BorderThickness = new Thickness(1)
            };
        }

        async Task AcaoDiagnostico(string acao)
        {
            if (ocupado || acao == null) return;
            if (acao.StartsWith("monitor:"))
            {
                int ok = await Dialogo("Ajustar a taxa do monitor",
                    Juntar(Paragrafo("Vamos abrir as configurações de vídeo do Windows. Em \"Taxa de atualização\", escolha " + acao.Substring(8) + " Hz e confirme."),
                           Paragrafo("Se a tela piscar ou ficar preta, espere 15 segundos: o Windows volta sozinho para a taxa anterior.", "#8B98AD")),
                    new OpcaoDialogo("Cancelar", "Botao", 0), new OpcaoDialogo("Abrir configurações", "BotaoPrimario", 1));
                if (ok == 1) { Otimizador.Abrir("ms-settings:display-advanced"); Toast("Depois de mudar, clique em Analisar de novo.", Ciano); }
            }
            else if (acao == "ram")
            {
                await Dialogo("Como ativar o XMP / DOCP",
                    Juntar(Paragrafo("O XMP (Intel) ou DOCP/EXPO (AMD) faz a memória rodar na velocidade da caixa. É uma opção da BIOS, então o FRAMEUP não muda isso sozinho."),
                           Lista(new[] { "Reinicie o PC e aperte Del (ou F2) na tela de inicialização.",
                                         "Procure por XMP, DOCP ou EXPO (às vezes em \"AI Tweaker\" ou \"OC\").",
                                         "Escolha o Perfil 1, salve e saia (geralmente F10).",
                                         "Se o PC não ligar, ele volta sozinho ao padrão depois de algumas tentativas." }, "#22D3EE")),
                    new OpcaoDialogo("Entendi", "Botao", 0));
            }
            else if (acao.StartsWith("url:"))
            {
                Otimizador.Abrir(acao.Substring(4));
            }
            else if (acao == "limpeza")
            {
                await LimpezaRapida();
                await Analisar();
            }
            else if (acao == "inicializacao")
            {
                await MostrarInicializacao();
            }
            else if (acao.StartsWith("ajuste:"))
            {
                if (motor.BackupInvalido) { await AoAbrir(); return; }
                var a = A(acao.Substring(7));
                Ocupado(true);
                var r = await Rodar(a.Aplicar, a.Nome);
                Ocupado(false);
                Toast(a.Nome + ": " + r.Detalhe, CorDo(r.Status));
                await Analisar();
            }
        }

        // ------------------------------------------------------------ montagem de conteúdo

        internal static StackPanel Juntar(params UIElement[] itens)
        {
            var p = new StackPanel();
            foreach (var i in itens) p.Children.Add(i);
            return p;
        }

        internal static StackPanel Linha(params UIElement[] itens)
        {
            var p = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var i in itens) p.Children.Add(i);
            return p;
        }

        internal static TextBlock Rotulo(string texto)
        {
            return new TextBlock { Text = texto, FontFamily = new FontFamily("Bahnschrift SemiBold, Segoe UI Semibold"), FontSize = 12, Foreground = new SolidColorBrush(Ciano) };
        }
    }
}
