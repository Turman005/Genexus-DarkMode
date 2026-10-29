// Xpz.Dark - menus e faixas de ferramentas WinForms (inclusive menus de contexto).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Xpz.Dark
{
    // =====================================================================
    //  Menus e faixas de ferramentas WinForms (incl. menus de contexto)
    // =====================================================================
    internal sealed class RendererEscuro : ToolStripProfessionalRenderer
    {
        public RendererEscuro() : base(new TabelaEscura()) { RoundedEdges = false; }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Paleta.Texto : Paleta.TextoApagado;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item != null && !e.Item.Enabled ? Paleta.TextoApagado : Paleta.Texto;
            base.OnRenderArrow(e);
        }
    }

    internal sealed class TabelaEscura : ProfessionalColorTable
    {
        public TabelaEscura() { UseSystemColors = false; }
        // Lidas a cada desenho: trocar o tema ao vivo muda os menus na hora.
        private static Color F { get { return Paleta.FundoPainel; } }
        private static Color E { get { return Paleta.FundoEntrada; } }
        private static Color R { get { return Paleta.Realce; } }
        private static Color B { get { return Paleta.Borda; } }
        private static Color S { get { return Paleta.Selecao; } }

        public override Color ToolStripGradientBegin { get { return F; } }
        public override Color ToolStripGradientMiddle { get { return F; } }
        public override Color ToolStripGradientEnd { get { return F; } }
        public override Color ToolStripBorder { get { return B; } }
        public override Color ToolStripDropDownBackground { get { return E; } }
        public override Color ToolStripContentPanelGradientBegin { get { return F; } }
        public override Color ToolStripContentPanelGradientEnd { get { return F; } }
        public override Color ToolStripPanelGradientBegin { get { return F; } }
        public override Color ToolStripPanelGradientEnd { get { return F; } }
        public override Color MenuStripGradientBegin { get { return F; } }
        public override Color MenuStripGradientEnd { get { return F; } }
        public override Color StatusStripGradientBegin { get { return F; } }
        public override Color StatusStripGradientEnd { get { return F; } }
        public override Color MenuBorder { get { return B; } }
        public override Color MenuItemBorder { get { return B; } }
        public override Color MenuItemSelected { get { return R; } }
        public override Color MenuItemSelectedGradientBegin { get { return R; } }
        public override Color MenuItemSelectedGradientEnd { get { return R; } }
        public override Color MenuItemPressedGradientBegin { get { return E; } }
        public override Color MenuItemPressedGradientMiddle { get { return E; } }
        public override Color MenuItemPressedGradientEnd { get { return E; } }
        public override Color ImageMarginGradientBegin { get { return E; } }
        public override Color ImageMarginGradientMiddle { get { return E; } }
        public override Color ImageMarginGradientEnd { get { return E; } }
        public override Color SeparatorDark { get { return B; } }
        public override Color SeparatorLight { get { return F; } }
        public override Color ButtonSelectedHighlight { get { return R; } }
        public override Color ButtonSelectedGradientBegin { get { return R; } }
        public override Color ButtonSelectedGradientMiddle { get { return R; } }
        public override Color ButtonSelectedGradientEnd { get { return R; } }
        public override Color ButtonSelectedBorder { get { return Paleta.BordaDestaque; } }
        public override Color ButtonPressedGradientBegin { get { return Paleta.Pressionado; } }
        public override Color ButtonPressedGradientMiddle { get { return Paleta.Pressionado; } }
        public override Color ButtonPressedGradientEnd { get { return Paleta.Pressionado; } }
        public override Color ButtonCheckedGradientBegin { get { return S; } }
        public override Color ButtonCheckedGradientMiddle { get { return S; } }
        public override Color ButtonCheckedGradientEnd { get { return S; } }
        public override Color CheckBackground { get { return S; } }
        public override Color CheckSelectedBackground { get { return S; } }
        public override Color CheckPressedBackground { get { return S; } }
        public override Color GripDark { get { return B; } }
        public override Color GripLight { get { return F; } }
        public override Color OverflowButtonGradientBegin { get { return F; } }
        public override Color OverflowButtonGradientMiddle { get { return F; } }
        public override Color OverflowButtonGradientEnd { get { return F; } }
    }
}
