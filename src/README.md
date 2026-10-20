# Fontes

`DeltaBuild.cs` gera diferenças com recortes de conteúdo. `DeltaApply.cs` aplica os comandos de cópia e inserção em gzip. `UnityPayload.cs` extrai os blocos do contêiner UnityFS original antes de aplicar a diferença. O manifesto verifica SHA-256 da base, de cada pacote e do resultado.

Para compilar o runtime em Windows com .NET Framework 4.x, execute nesta pasta:

```
csc.exe /nologo /optimize+ /target:library /out:AeonsEndPatch.dll /reference:System.IO.Compression.dll DeltaApply.cs UnityPayload.cs
```

`PortugueseSupport.cs` e `LabelTranslations.cs` são o suporte de fonte, encaixe e rótulos dinâmicos. Dependem das bibliotecas originais do Unity/TextMeshPro do jogo para compilação; essas bibliotecas não são redistribuídas neste repositório. As rotinas de diagnóstico não são chamadas pelo jogo distribuído. Os patches publicados também contêm alterações de texto nos recursos e nas bibliotecas, produzidas a partir da base compatível e verificadas pelo manifesto.

Os scripts na raiz dependem dos patches e dos componentes contidos no ZIP da release. Baixar o código-fonte do GitHub não substitui o pacote da release.
