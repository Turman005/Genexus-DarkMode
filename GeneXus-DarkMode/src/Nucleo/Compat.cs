// Versao de compatibilidade do pacote (atributo PackageCompatibility do GeneXus).
// 143920 = GeneXus 18 (arquivo packages.143920.xml da instalacao). scripts/Build.ps1 -GeneXus <pasta> le o numero
// da instalacao e regrava esta linha; se o GeneXus mudar o numero, o pacote antigo e ignorado (nao quebra a IDE).
namespace GxDarkTheme
{
    public static class Compat
    {
        public const int Versao = 143920;
    }
}
