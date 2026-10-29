#!/usr/bin/env python3
"""Previas (replica da IDE) por tema. As cores e os icones passam pelo MESMO M() da DLL
(testes/box TestesBox --map), entao a previa mostra exatamente o que a extensao calcula.
uso: python3 tools/previa.py <TestesBox.dll> <pasta-saida>"""
import os, sys, struct, subprocess, tempfile
from PIL import Image, ImageDraw, ImageFont

AQUI = os.path.dirname(os.path.abspath(__file__)); RAIZ = os.path.dirname(AQUI)
ICONS = os.path.join(RAIZ, 'resources', 'icons')
DLL, OUT = sys.argv[1], sys.argv[2]
DOTNET = os.environ.get('DOTNET', 'dotnet')
TEMAS = [('next', 'GeneXus Next'), ('ardosia', 'Ardósia'), ('grafite', 'Grafite'), ('meianoite', 'Meia-noite'),
         ('ametista', 'Ametista'), ('brasa', 'Brasa'), ('ambar', 'Âmbar'), ('oliva', 'Oliva')]
NEXT = dict(Editor=(17,21,28), Panel=(24,31,42), Chrome=(36,45,60), Menu=(24,31,42), Input=(31,39,52), Hover=(52,72,96),
            Button=(44,60,80), Border=(51,61,71), Accent=(44,141,206), Sel=(44,60,80), Fg=(220,226,235), FgBright=(240,244,250),
            FgDim=(138,144,155), FgSoft=(190,198,210), FgDisabled=(150,157,168), Link=(91,167,255), HotBg=(52,72,96),
            HotBorder=(78,104,136), MenuHot=(31,78,121), Tab=(31,39,52), CurLine=(28,35,46))
SINT_NEXT = dict(texto='#D4D4D4', palavra='#569CD6', controle='#C586C0', tipo='#4EC9B0', funcao='#DCDCAA',
                 variavel='#9CDCFE', string='#CE9178', numero='#B5CEA8', comentario='#6A9955')

def mapear(tema, argb):
    with tempfile.TemporaryDirectory() as d:
        i, o = os.path.join(d, 'i.bin'), os.path.join(d, 'o.bin')
        open(i, 'wb').write(struct.pack('<%di' % len(argb), *argb))
        subprocess.check_call([DOTNET, DLL, '--map', tema, i, o])
        b = open(o, 'rb').read()
        return list(struct.unpack('<%di' % (len(b) // 4), b))

def argb(c, a=255): v = (a << 24) | (c[0] << 16) | (c[1] << 8) | c[2]; return v - (1 << 32) if v >= 1 << 31 else v
def rgb(v): v &= 0xFFFFFFFF; return ((v >> 16) & 255, (v >> 8) & 255, v & 255)

def folha(tema, nome):
    im = Image.open(os.path.join(ICONS, nome)).convert('RGBA')
    px = [argb(p[:3], p[3]) for p in (im.get_flattened_data() if hasattr(im, "get_flattened_data") else im.getdata())]
    px = mapear(tema, px)
    im2 = Image.new('RGBA', im.size)
    im2.putdata([(*rgb(v), (v >> 24) & 255) for v in px])
    return im2

def hexc(h): return tuple(int(h[i:i+2], 16) for i in (1, 3, 5))
sint = {}
for l in open(sys.argv[3] if len(sys.argv) > 3 else os.path.join(RAIZ, '..', 'out', 'sintaxe.txt')):
    t, k, h = l.split(); sint.setdefault(t, {})[k] = hexc(h)

nomes = [l.split('|')[2] for l in open(os.path.join(ICONS, 'icons.txt')) if l.startswith('I|')]
def F(sz, bold=False):
    return ImageFont.truetype('/usr/share/fonts/truetype/sand-box/google/Inter/Inter-VariableFont_opsz,wght.ttf', sz) if not bold else \
        ImageFont.truetype('/usr/share/fonts/truetype/sand-box/google/Inter/Inter-VariableFont_opsz,wght.ttf', sz)
MONO = ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf', 13)
f12, f13, f11 = F(12), F(13), F(11)

os.makedirs(OUT, exist_ok=True)
for tid, tnome in TEMAS:
    chaves = list(NEXT)
    pal = dict(zip(chaves, [rgb(v) for v in mapear(tid, [argb(NEXT[k]) for k in chaves])]))
    S = {k: hexc(v) for k, v in SINT_NEXT.items()} if tid == 'next' else sint[tid]
    s16, s24 = folha(tid, 'next_enabled_16.png'), folha(tid, 'next_enabled_24.png')
    def ic(n, sz=16):
        i = nomes.index(n); sh = s16 if sz == 16 else s24
        return sh.crop((i * sz, 0, i * sz + sz, sz))
    W, H = 1280, 800
    im = Image.new('RGB', (W, H), pal['Panel']); d = ImageDraw.Draw(im)
    # titulo + menu
    d.rectangle((0, 0, W, 30), fill=pal['Chrome']); d.text((12, 8), 'RecursosHumanos - GeneXus 18', font=f12, fill=pal['Fg'])
    d.line((W - 124, 15, W - 114, 15), fill=pal['FgSoft']); d.rectangle((W - 84, 10, W - 74, 20), outline=pal['FgSoft'])
    d.line((W - 44, 10, W - 34, 20), fill=pal['FgSoft']); d.line((W - 44, 20, W - 34, 10), fill=pal['FgSoft'])
    d.rectangle((0, 30, W, 56), fill=pal['Chrome'])
    x = 8
    menus = ['File', 'Edit', 'View', 'Knowledge Manager', 'Build', 'Tools', 'Window', 'Help']
    xtools = 0
    for m in menus:
        w = int(d.textlength(m, font=f12)) + 16
        if m == 'Tools': d.rectangle((x, 31, x + w, 55), fill=pal['MenuHot']); xtools = x
        d.text((x + 8, 37), m, font=f12, fill=pal['Fg']); x += w
    # sol (ambar) a direita da barra de menus
    sa = Image.open(os.path.join(ICONS, 'toggle_B_sun_16.png')).convert('L')
    sol = Image.new('RGBA', sa.size, (0xE5, 0xA5, 0x4B, 255)); sol.putalpha(sa)
    im.paste(sol, (W - 30, 35), sol)
    # barra de ferramentas
    d.rectangle((0, 56, W, 86), fill=pal['Chrome']); d.line((0, 86, W, 86), fill=pal['Border'])
    x = 10
    for n in ['menus/new-object', 'menus/open-object', 'menus/save', 'menus/save-all', None, 'menus/cut', 'menus/copy', 'menus/paste',
              'menus/undo', 'menus/redo', None, 'menus/find', None, 'menus/build', 'menus/build-all', 'menus/run', 'menus/create-database-tables',
              None, 'window-tools/properties', 'window-tools/output', 'window-tools/toolbox', 'window-tools/teamdev', 'window-tools/history']:
        if n is None: d.line((x + 2, 62, x + 2, 80), fill=pal['Border']); x += 10; continue
        if n == 'menus/build': d.rectangle((x - 3, 59, x + 21, 83), fill=pal['HotBg'])
        i = ic(n); im.paste(i, tuple(int(v) for v in (x + 2, 63)), i); x += 28
    # KB Explorer
    LW = 270
    d.rectangle((0, 87, LW, H - 26), fill=pal['Panel']); d.rectangle((0, 87, LW, 111), fill=pal['Chrome'])
    d.text((10, 92), 'KB Explorer', font=f12, fill=pal['FgBright'])
    d.rectangle((8, 118, LW - 8, 142), fill=pal['Input'], outline=pal['Border']); d.text((16, 123), 'Pesquisar', font=f12, fill=pal['FgDim'])
    arv = [(0, 'objects/folder-open', 'Root Module'), (1, 'objects/module', 'RecursosHumanos'), (2, 'objects/transaction', 'Funcionario'),
           (2, 'objects/procedure', 'CalcularFolha'), (2, 'objects/webpanel', 'WPHome'), (2, 'objects/structured-data-type', 'SDTEndereco'),
           (2, 'objects/domain', 'CPF'), (2, 'objects/data-provider', 'DPMenu'), (2, 'objects/attribute', 'FuncionarioId'),
           (2, 'patterns/work-with-web', 'WorkWithFuncionario'), (1, 'objects/folder', 'Imagens'), (2, 'objects/image', 'LogoEmpresa'),
           (1, 'objects/theme', 'TemaCorporativo'), (1, 'objects/masterpage', 'MasterPage'), (1, 'objects/external-object', 'ApiCliente')]
    y = 150
    for nv, n, t in arv:
        if t == 'CalcularFolha': d.rectangle((0, y - 2, LW, y + 20), fill=pal['Sel'])
        i = ic(n); im.paste(i, tuple(int(v) for v in (12 + nv * 18, y + 1)), i); d.text((34 + nv * 18, y + 1), t, font=f13, fill=pal['Fg']); y += 24
    d.line((LW, 87, LW, H - 26), fill=pal['Border'])
    # abas + editor
    RX = W - 300
    d.rectangle((LW + 1, 87, RX, 113), fill=pal['Chrome'])
    x = LW + 1
    for t, n, at in (('CalcularFolha', 'objects/procedure', True), ('Funcionario', 'objects/transaction', False), ('WPHome', 'objects/webpanel', False)):
        w = int(d.textlength(t, font=f12)) + 46
        d.rectangle((x, 87, x + w, 113), fill=pal['Editor'] if at else pal['Chrome'])
        if at: d.line((x, 87, x + w, 87), fill=pal['Accent'], width=2)
        i = ic(n); im.paste(i, tuple(int(v) for v in (x + 8, 92)), i); d.text((x + 28, 93), t, font=f12, fill=pal['FgBright'] if at else pal['FgSoft']); x += w + 1
    d.rectangle((LW + 1, 113, RX, 139), fill=pal['Panel'])
    x = LW + 8
    for t, n in (('Source', 'objects-parts/rules'), ('Rules', 'objects-parts/rules'), ('Conditions', 'objects-parts/conditions'), ('Variables', 'objects-parts/variables'), ('Documentation', 'patterns/documentation')):
        w = int(d.textlength(t, font=f11)) + 34
        if t == 'Source': d.rectangle((x - 4, 115, x + w - 8, 139), fill=pal['Editor'])
        i = ic(n); im.paste(i, tuple(int(v) for v in (x, 119)), i); d.text((x + 20, 120), t, font=f11, fill=pal['Fg'] if t == 'Source' else pal['FgSoft']); x += w
    EY = 140; EB = H - 200
    d.rectangle((LW + 1, EY, RX, EB), fill=pal['Editor'])
    cod = [[('// Calcula a folha do mes', 'comentario')],
           [('For each', 'palavra'), (' Funcionario', 'tipo')],
           [('    where', 'palavra'), (' FuncionarioAtivo = ', 'texto'), ('True', 'palavra')],
           [('    &Salario', 'variavel'), (' = ', 'texto'), ('Round', 'funcao'), ('(FuncionarioSalario * ', 'texto'), ('1.08', 'numero'), (', ', 'texto'), ('2', 'numero'), (')', 'texto')],
           [('    If', 'controle'), (' &Salario > ', 'texto'), ('&Teto', 'variavel')],
           [('        &Mensagem', 'variavel'), (' = ', 'texto'), ('"Acima do teto: "', 'string'), (' + ', 'texto'), ('&Salario', 'variavel'), ('.ToString()', 'funcao')],
           [('        msg', 'funcao'), ('(&Mensagem)', 'texto')],
           [('    EndIf', 'controle')],
           [('    New', 'palavra')],
           [('        FolhaFuncionarioId', 'texto'), (' = FuncionarioId', 'texto')],
           [('        FolhaValor', 'texto'), (' = ', 'texto'), ('&Salario', 'variavel')],
           [('    EndNew', 'palavra')],
           [('EndFor', 'palavra')],
           [('Commit', 'palavra')]]
    y = EY + 10
    for ln, linha in enumerate(cod, 1):
        if ln == 4: d.rectangle((LW + 40, y - 2, RX, y + 17), fill=pal['CurLine'])
        d.text((LW + 10, y), '%2d' % ln, font=MONO, fill=pal['FgDim'])
        x = LW + 46
        for t, k in linha:
            d.text((x, y), t, font=MONO, fill=S[k]); x += d.textlength(t, font=MONO)
        y += 21
    # Output
    d.rectangle((LW + 1, EB, RX, EB + 26), fill=pal['Chrome']); d.line((LW + 1, EB, RX, EB), fill=pal['Border'])
    i = ic('window-tools/output'); im.paste(i, tuple(int(v) for v in (LW + 8, EB + 5)), i); d.text((LW + 30, EB + 6), 'Output', font=f12, fill=pal['FgBright'])
    d.rectangle((LW + 1, EB + 26, RX, H - 26), fill=pal['Panel'])
    for k, (t, c) in enumerate((('========== Build CalcularFolha started ==========', 'Fg'), ('Specifying procedure CalcularFolha', 'FgSoft'),
                                ('Generating CalcularFolha', 'FgSoft'), ('Build CalcularFolha Success', 'Fg'), ('Abrir log completo', 'Link'))):
        d.text((LW + 12, EB + 34 + k * 19), t, font=MONO if c != 'Link' else f12, fill=pal[c])
    # Properties
    d.line((RX, 87, RX, H - 26), fill=pal['Border'])
    d.rectangle((RX + 1, 87, W, 111), fill=pal['Chrome']); i = ic('window-tools/properties'); im.paste(i, tuple(int(v) for v in (RX + 8, 92)), i)
    d.text((RX + 30, 92), 'Properties', font=f12, fill=pal['FgBright'])
    d.rectangle((RX + 1, 111, W, 137), fill=pal['Panel'])
    for k, n in enumerate(('window-tools/category-groups', 'window-tools/alphabetical-order', 'window-tools/filter')):
        if k == 0: d.rectangle((RX + 6 + k * 26, 113, RX + 28 + k * 26, 135), fill=pal['HotBg'], outline=pal['HotBorder'])
        i = ic(n); im.paste(i, tuple(int(v) for v in (RX + 9 + k * 26, 116)), i)
    props = [('Procedure: CalcularFolha', None), ('Name', 'CalcularFolha'), ('Description', 'Calcular Folha'), ('Folder', 'RecursosHumanos'),
             ('Main program', 'False'), ('Call protocol', 'Internal'), ('Generator', 'Default (C# Web)'), ('Execute in new LUW', 'False'),
             ('Commit on exit', 'Yes'), ('Encrypt URL parameters', 'Session Key'), ('Object Visibility', 'Public')]
    y = 140
    for n, v in props:
        if v is None:
            d.rectangle((RX + 1, y, W, y + 22), fill=pal['Chrome']); d.text((RX + 10, y + 4), n, font=f12, fill=pal['FgBright'])
        else:
            if n == 'Generator': d.rectangle((RX + 1, y, W, y + 22), fill=pal['Sel'])
            d.text((RX + 14, y + 4), n, font=f12, fill=pal['Fg']); d.text((RX + 160, y + 4), v, font=f12, fill=pal['FgSoft'])
            d.line((RX + 150, y, RX + 150, y + 22), fill=pal['Border'])
        d.line((RX + 1, y + 22, W, y + 22), fill=pal['Border']); y += 23
    d.text((RX + 10, y + 10), 'Generator', font=F(12), fill=pal['FgBright'])
    d.text((RX + 10, y + 30), 'Gerador usado para este objeto.', font=f11, fill=pal['FgDim'])
    # menu Tools aberto com o submenu Tema escuro
    MX, MY, MW = xtools, 56, 250
    itens = ['Options...', 'Customize...', 'External Tools', None, 'Tema escuro']
    mh = sum(9 if t is None else 26 for t in itens) + 4
    d.rectangle((MX, MY, MX + MW, MY + mh), fill=pal['Menu'], outline=pal['Border'])
    y = MY + 2
    for t in itens:
        if t is None: d.line((MX + 30, y + 4, MX + MW - 6, y + 4), fill=pal['Border']); y += 9; continue
        if t == 'Tema escuro': d.rectangle((MX + 2, y, MX + MW - 2, y + 25), fill=pal['MenuHot']); d.text((MX + MW - 18, y + 5), '›', font=f13, fill=pal['Fg']); ty = y
        d.text((MX + 34, y + 6), t, font=f12, fill=pal['Fg']); y += 26
    SX, SW = MX + MW - 2, 270
    sub = [n for _, n in TEMAS] + [None, 'Modo escuro ligado (Ctrl+Shift+F12)', None, 'Configurações...']
    sh = sum(9 if t is None else 26 for t in sub) + 4
    d.rectangle((SX, ty - 2, SX + SW, ty - 2 + sh), fill=pal['Menu'], outline=pal['Border'])
    y = ty
    for t in sub:
        if t is None: d.line((SX + 30, y + 4, SX + SW - 6, y + 4), fill=pal['Border']); y += 9; continue
        if t == tnome: d.text((SX + 10, y + 5), '✓', font=f13, fill=pal['Fg'])
        if t.startswith('Modo'): d.text((SX + 10, y + 5), '✓', font=f13, fill=pal['Fg'])
        d.text((SX + 34, y + 6), t, font=f12, fill=pal['FgBright'] if t == tnome else pal['Fg']); y += 26
    # status
    d.rectangle((0, H - 26, W, H), fill=pal['Chrome']); d.line((0, H - 26, W, H - 26), fill=pal['Border'])
    d.text((10, H - 20), 'Pronto', font=f12, fill=pal['FgSoft'])
    d.text((W - 360, H - 20), 'GxDarkTheme 2.0 · tema ' + tnome + ' (prévia)', font=f12, fill=pal['FgSoft'])
    im.save(os.path.join(OUT, 'previa-%s.png' % tid))
    print('ok', tid)
