# PipeMasterM para Revit 2025

Solução C# do add-in PipeMasterM, versão `1.0.6.0`, destinada ao Revit 2025 e ao .NET 8 para Windows.

## Pré-requisitos

- .NET SDK 8.0.4xx
- Autodesk Revit 2025 instalado
- Acesso ao NuGet para restaurar WebView2 `1.0.3967.48`

Por padrão, o projeto procura as APIs do Revit em:

`C:\Program Files\Autodesk\Revit 2025`

Para usar outro local:

```powershell
dotnet build .\PipeMasterM.sln -p:RevitInstallDir="D:\Autodesk\Revit 2025"
```

## Compilação

```powershell
dotnet restore .\PipeMasterM.sln
dotnet build .\PipeMasterM.sln -c Debug --no-restore
```

O assembly é gerado em `bin\Debug\net8.0-windows\PipeMasterM.dll`.

## Compilação de teste sem login

Para validar os comandos em um ambiente controlado, use a configuração `Test`:

```powershell
dotnet build .\PipeMasterM.sln -c Test --no-restore
```

Essa configuração define `PIPEMASTER_TEST_MODE`, libera os comandos quando existe um documento Revit ativo, evita chamadas ao servidor de validação e altera o botão de acesso para `Modo Teste`. As configurações `Debug` e `Release` mantêm o login normal.

## Bundle independente

O repositório contém os ícones, os manifestos Autodesk e todas as dependências próprias do plugin. Para produzir um bundle sem utilizar nenhum arquivo da instalação original do PipeMaster:

```powershell
powershell -ExecutionPolicy Bypass -File .\Build-Bundle.ps1 -Configuration Test
```

Se o SDK .NET 8 não estiver no `PATH`, informe o executável:

```powershell
powershell -ExecutionPolicy Bypass -File .\Build-Bundle.ps1 -Configuration Test -DotNetPath "C:\caminho\dotnet.exe"
```

O resultado é criado em `dist\PipeMasterM.bundle`. Ele contém somente a DLL compilada, o arquivo `.deps.json`, manifestos, ícones e os componentes WPF/Core do WebView2 com seu carregador nativo. PDBs, documentação XML, WebView2 WinForms e artefatos de build não são distribuídos. O único requisito externo em execução é o Autodesk Revit 2025 e suas APIs.

## Identidade visual

A aba do plugin no ribbon do Revit se chama `CEP-HS`. A interface usa laranja `#F57C00` como cor principal. Os ícones preservam integralmente o desenho, o fundo, a transparência, a espessura e as proporções dos arquivos originais; somente o matiz dos pixels roxos é convertido para o matiz laranja da marca. Os ícones grandes de 32 × 32 pixels e suas versões pequenas proporcionais com sufixo `_16` ficam em `Assets\Icones`. O Revit recebe cada tamanho separadamente para evitar redução automática e perda de nitidez.

Para reaplicar a identidade visual aos ícones originais:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Apply-Branding.ps1
```

O processo mantém alfa, saturação e luminosidade durante a troca de matiz e não aplica afinação, engrossamento, redesenho ou nitidez artificial.

## Estado da reconstrução

- Versão do assembly preservada: `1.0.6.0`
- Todos os 114 tipos de nível superior do projeto estão alcançáveis a partir das entradas do Revit, recursos WPF ou serialização
- Recursos WPF e o recurso vazio de `JanelaRotacaoWPF` recompilados
- Artefatos inválidos de parâmetros `ref` corrigidos conforme o IL original
- Código e dependências sem uso removidos, incluindo o antigo caminho CAD/SkiaSharp
- Compilações `Test` e `Release` validadas com zero erros

Os avisos remanescentes da compilação são conflitos de versão das referências fornecidas pelo Revit. Em ambientes sem acesso ao NuGet, a auditoria de vulnerabilidades também pode emitir `NU1900`; nenhum deles representa uma dependência do PipeMaster ausente.

## Observação sobre a API do Revit

O assembly instalado foi originalmente compilado contra `RevitAPI` e `RevitAPIUI` `25.4.0.0`. A instalação disponível durante esta reconstrução fornece `25.0.0.0`. Todos os símbolos utilizados estão presentes e a solução compila, mas a equivalência operacional completa deve ser validada dentro do Revit antes de substituir o plugin instalado.
