// FRAMEUP - lista de ajustes da aba Personalizada e dos perfis prontos.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Frameup
{
    public enum Nivel { Recomendado, Avancado, Opcional }

    public class Ajuste
    {
        public string Id, Categoria, Nome, Descricao;
        public Nivel Nivel;
        public bool ApagaDeVez;                 // ação sem volta (lixeira): sempre pede confirmação
        public Func<Resultado> Aplicar;
        public Func<bool> Ativo;                // null = ação única (limpeza, DNS...), sem estado
        public Func<bool> PodeDesfazer;
        public Func<Resultado> Desfazer;
    }

    public class PerfilPronto
    {
        public string Id, Nome, Descricao;
        public HashSet<string> Ajustes;
    }

    public static class Catalogo
    {
        // O que a tela inicial roda no botão OTIMIZAR AGORA.
        public static readonly string[] Inicio =
            { "ponto", "plano", "usb", "modojogo", "gravacao", "prioridade", "visual", "dicas", "limpeza", "dns" };

        public static List<Ajuste> Criar(Otimizador m, Action<long> somarBytes)
        {
            var lista = new List<Ajuste>();
            Action<string, string, string, string, Nivel, Func<Resultado>, Func<bool>> registro =
                (id, categoria, nome, descricao, nivel, aplicar, ativo) => lista.Add(new Ajuste
                {
                    Id = id, Categoria = categoria, Nome = nome, Descricao = descricao, Nivel = nivel,
                    Aplicar = aplicar, Ativo = ativo,
                    PodeDesfazer = () => m.TemBackupDoGrupo(id),
                    Desfazer = () => m.DesfazerGrupo(id)
                });
            Action<string, string, string, string, Nivel, Func<Resultado>> acao =
                (id, categoria, nome, descricao, nivel, aplicar) => lista.Add(new Ajuste
                {
                    Id = id, Categoria = categoria, Nome = nome, Descricao = descricao, Nivel = nivel, Aplicar = aplicar
                });

            acao("ponto", "Segurança", "Ponto de restauração", "Cria um ponto de restauração do Windows antes de tudo",
                Nivel.Recomendado, () => m.CriarPontoRestauracao());

            lista.Add(new Ajuste
            {
                Id = "plano", Categoria = "Energia", Nome = "Plano de energia", Nivel = Nivel.Recomendado,
                Descricao = "Alto desempenho enquanto você joga",
                Aplicar = () => m.AtivarPlanoDesempenho(), Ativo = Otimizador.PlanoFrameupAtivo,
                PodeDesfazer = () => m.PodeDesfazerPlano(), Desfazer = () => m.DesfazerPlano()
            });
            lista.Add(new Ajuste
            {
                Id = "usb", Categoria = "Energia", Nome = "USB sem economia de energia", Nivel = Nivel.Recomendado,
                Descricao = "Mouse, teclado e headset param de desconectar sozinhos",
                Aplicar = () => m.DesligarSuspensaoUsb(), Ativo = Otimizador.UsbSemSuspensao,
                PodeDesfazer = Otimizador.UsbAlterado, Desfazer = () => m.ReativarSuspensaoUsb()
            });

            registro("modojogo", "Jogos", "Modo de Jogo", "O Windows prioriza o jogo aberto",
                Nivel.Recomendado, () => m.LigarModoDeJogo(), Otimizador.ModoDeJogoLigado);
            registro("gravacao", "Jogos", "Gravação em segundo plano", "Desliga a gravação automática do Xbox Game Bar",
                Nivel.Recomendado, () => m.DesligarGravacaoFundo(), Otimizador.GravacaoDesligada);
            registro("prioridade", "Jogos", "Prioridade para jogos", "CPU e GPU dão preferência ao jogo",
                Nivel.Recomendado, () => m.PriorizarJogos(), Otimizador.PrioridadeAtiva);

            registro("visual", "Windows", "Visual mais leve", "Menos transparência e animações",
                Nivel.Recomendado, () => m.AliviarVisual(), Otimizador.VisualLeveAtivo);
            registro("dicas", "Windows", "Sem dicas e anúncios", "Desliga sugestões, dicas e apps instalados sozinhos",
                Nivel.Recomendado, () => m.DesligarSugestoes(), Otimizador.SugestoesDesligadas);
            registro("efeitos", "Windows", "Efeitos visuais mínimos", "Visual simples do Windows, ideal para PC fraco",
                Nivel.Avancado, () => m.EfeitosDesempenho(), Otimizador.EfeitosDesempenhoAtivos);
            registro("appsfundo", "Windows", "Apps em segundo plano", "Impede apps da Loja de rodar escondidos",
                Nivel.Opcional, () => m.DesligarAppsSegundoPlano(), Otimizador.AppsSegundoPlanoDesligados);

            registro("mouse", "Mouse e teclado", "Mira consistente", "Desliga a aceleração do mouse",
                Nivel.Opcional, () => m.DesligarAceleracaoMouse(), Otimizador.MouseSemAceleracao);
            registro("teclado", "Mouse e teclado", "Teclado mais rápido", "Tecla segurada repete mais rápido",
                Nivel.Opcional, () => m.TecladoRapido(), Otimizador.TecladoRapidoAtivo);

            acao("dns", "Rede", "Cache de DNS", "Limpa endereços guardados de sites e servidores",
                Nivel.Recomendado, () => m.LimparCacheDns());
            registro("rede", "Rede", "Rede sem limitação", "O Windows para de segurar pacotes enquanto toca áudio",
                Nivel.Avancado, () => m.RedeSemLimite(), Otimizador.RedeSemLimiteAtiva);

            acao("limpeza", "Limpeza", "Arquivos temporários", "Temporários e relatórios de erro antigos",
                Nivel.Recomendado, () => { long b; var r = m.LimparTemporarios(out b); somarBytes(b); return r; });
            acao("shaders", "Limpeza", "Cache de shaders", "Os jogos refazem o cache; o 1º carregamento demora mais",
                Nivel.Avancado, () => { long b; var r = m.LimparShaders(out b); somarBytes(b); return r; });
            lista.Add(new Ajuste
            {
                Id = "lixeira", Categoria = "Limpeza", Nome = "Esvaziar a lixeira", Nivel = Nivel.Opcional, ApagaDeVez = true,
                Descricao = "Apaga de vez o que está na lixeira",
                Aplicar = () => { long b; var r = m.EsvaziarLixeira(out b); somarBytes(b); return r; }
            });
            return lista;
        }

        public static List<PerfilPronto> Perfis(List<Ajuste> catalogo)
        {
            var recomendados = catalogo.Where(a => a.Nivel == Nivel.Recomendado).Select(a => a.Id).ToList();
            return new List<PerfilPronto>
            {
                new PerfilPronto
                {
                    Id = "recomendado", Nome = "Recomendado", Descricao = "Os ajustes seguros que valem para todo PC.",
                    Ajustes = new HashSet<string>(recomendados)
                },
                new PerfilPronto
                {
                    Id = "maximo", Nome = "Máximo desempenho", Descricao = "Recomendado + avançados, para PC fraco ou competitivo.",
                    Ajustes = new HashSet<string>(recomendados.Concat(new[] { "efeitos", "appsfundo", "mouse", "rede", "shaders" }))
                },
                new PerfilPronto
                {
                    Id = "notebook", Nome = "Notebook", Descricao = "Sem o plano de alto desempenho, para poupar bateria.",
                    Ajustes = new HashSet<string>(recomendados.Where(id => id != "plano" && id != "usb").Concat(new[] { "appsfundo" }))
                }
            };
        }
    }
}
