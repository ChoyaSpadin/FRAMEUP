# ⚡ FRAMEUP — otimização gamer segura para Windows

Aplicativo desktop para Windows 10 e 11 que deixa o PC mais leve para jogar em **1 clique**. Todo ajuste pode ser **desfeito**.

**[⬇️ Baixar a versão mais recente (Releases)](../../releases/latest)**

## ✨ Funcionalidades

- **Início**: otimização completa com o botão *OTIMIZAR AGORA*, criando antes um ponto de restauração do Windows.
- **Personalizada**: 17 ajustes em 7 categorias (Energia, Jogos, Windows, Mouse e teclado, Rede, Limpeza, Segurança), com 3 perfis prontos (*Recomendado*, *Máximo desempenho* e *Notebook*) e um botão para desfazer cada ajuste.
- **Diagnóstico**: nota de 0 a 100 para o PC, com a lista de problemas encontrados e o que fazer em cada um.
- **Desfazer tudo**: devolve todas as configurações ao valor de antes.

## 🛡️ O que o FRAMEUP não faz

- Não desliga antivírus, Windows Defender nem Windows Update.
- Não mexe em drivers, serviços do sistema nem BIOS.
- Não instala nada e não fica rodando depois de fechado.
- Não coleta nem envia nenhum dado.

## 🛠️ Tecnologias

- **C#** com **WPF** (interface em XAML)
- **.NET Framework 4.8**, que já vem com o Windows, então não precisa instalar nada
- WMI (`System.Management`) para o diagnóstico de hardware

## 📁 Estrutura

| Arquivo | Função |
|---|---|
| `App.cs` | Inicialização, telas e navegação entre as abas |
| `Janela.xaml` | Layout da interface |
| `Catalogo.cs` | Lista dos ajustes e dos perfis prontos |
| `Otimizador.cs` | Aplica, verifica e desfaz cada ajuste (com backup do valor original) |
| `Diagnostico.cs` | Análise do PC e cálculo da nota |
| `Limpeza.cs` | Limpeza de temporários, cache de shaders e lixeira |
| `Captura.cs` | Modo que gera as capturas de tela de divulgação |

## 🔨 Como compilar

Não precisa de Visual Studio: o script usa o compilador C# que já vem no Windows.

```bat
compilar.bat
```

O `FRAMEUP.exe` é gerado na pasta acima desta. O programa pede permissão de administrador ao abrir, porque altera configurações do sistema.

> Se o Windows mostrar "O Windows protegeu o computador" (SmartScreen), clique em **Mais informações → Executar assim mesmo**. Isso acontece com qualquer programa novo que ainda não tem assinatura digital.
