"""The location book: one morning, eight places, one frame each.

    python3 Tools/docs/locations.py        -> 1079-lokacii.pdf beside the project
    python3 Tools/docs/locations.py --md   -> docs/LOCATIONS.md, the same text without the pictures

The pictures come from the game itself, not from a screenshot key: build, run `1079 -shots`, walk the site
viewer round with F3, and every view writes Shots/NN-id.png. The shots run pins the clock to 11:20 on
2 February 1959 and clears the sky, so the book is one hour of one day and two runs can be compared.
"""
import os
import sys
from reportlab.lib.pagesizes import A4, landscape
from reportlab.lib.units import mm
from reportlab.lib import colors
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen import canvas
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SHOTS = os.path.join(ROOT, 'Shots')
OUT = os.path.join(ROOT, '1079-lokacii.pdf')
JPG = os.path.join(SHOTS, '.jpg')   # the PNGs the game writes are 4-5 MB each; nothing here needs lossless

D = '/usr/share/fonts/truetype/dejavu/'
pdfmetrics.registerFont(TTFont('Body', D + 'DejaVuSerif.ttf'))
pdfmetrics.registerFont(TTFont('BodyB', D + 'DejaVuSerif-Bold.ttf'))
pdfmetrics.registerFont(TTFont('Cap', D + 'DejaVuSansCondensed.ttf'))
pdfmetrics.registerFont(TTFont('CapB', D + 'DejaVuSansCondensed-Bold.ttf'))

PAGE = landscape(A4)
W, H = PAGE
INK = colors.Color(.11, .12, .15)
GREY = colors.Color(.42, .44, .48)
RED = colors.Color(.58, .13, .11)
PAPER = colors.Color(.97, .965, .95)
GHOST = colors.Color(.44, .55, .72)

DAY = 'Утро 2 февраля 1959 года, 11:20'

# (file, title, subtitle, [paragraph, ...]) — two short paragraphs a page, no more
PLACES = [
    ('01-tent.png', 'Палатка на склоне Холатчахля',
     '61°45′30,8″ с. ш.  59°25′46,0″ в. д.  ·  ≈900 м  ·  1,4 км до кедра, 300 м выше него',
     ['Две четырёхместные «Турист», сшитые по двойному шву: конёк 4,33 м, скат 1,14 м. Конёк вдоль '
      'горизонтали, вход на юг, под полом восемь пар лыж. Площадка врезана в склон на полметра.',
      'Стоит, но задний конец уже осел: заднюю стойку в ту ночь разрезали на куски, они лежат поверх вещей. '
      'Снега на скате пара сантиметров — это одна ночь, а не три недели, как на поисковых фото 26 февраля.']),

    ('02-tent-cuts.png', 'Пять повреждений, а не три',
     'Экспертиза Чуркиной 3.IV.1959 (листы 303–304) и протокол № 199 от 16.IV.1959',
     ['Три разреза изнутри — 0,32, 0,89 и 0,42 м. И два вырванных куска, 0,88 × 0,70 и 0,84 × 0,66 м: вместе '
      'они больше, чем все три разреза. Пересказы сводят все пять к «разрезам» — поэтому вырывы тут и появились.',
      'Это дыры, а не рисунок на ткани: у каждой есть кромка уходящего внутрь брезента и темнота за ней. '
      'У нижнего выреза кусок оторвался не целиком — угол свисает.']),

    ('10-dyatlov.png', 'Склон: линия тел',
     'Колмогорова ≈630 м, Слободин ≈480 м, Дятлов ≈300 м от кедра — почти на одной прямой',
     ['Все трое лежали головой к палатке — так они и развёрнуты. Дятлова нашли 26 февраля у берёзы, на самой '
      'кромке леса: выше отсюда открытый склон до самой палатки.',
      'Контур, а не тело. Он говорит то, чего модель сказать не может: это реконструкция человека, стоящего '
      'на месте, а не фотография. Имя лежит на снегу рядом.']),

    ('03-cedar.png', 'Кедр у кромки леса',
     '61°45′53,2″ с. ш.  59°27′17,8″ в. д.  ·  634 м  ·  1,4 км от палатки',
     ['Ветки обломаны до 4,5 м со стороны склона, откуда шли: ломали, повисая всем телом, в кроне никто не был. '
      'Под деревом костёр в ямке, головешки до 80 мм. Горел полтора-два часа и, по словам Согрина, согреть не мог.',
      'В радиусе 20 м ножом срезано около двадцати пихточек. Самих веток здесь нет — они ушли на настил в овраге.']),

    ('04-p4.png', 'Овраг: настил и место четверых',
     '≈50 м ниже кедра  ·  отсчитан от камня P4, ориентира экспедиции КАН',
     ['Четырнадцать пихтовых стволов и одна берёза, площадка 2 × 1,5 м слоем 20–30 см, по углам четыре вещи. '
      'Сложен в ту же ночь — поэтому он здесь физически, а не призраком.',
      'Единственное место не из этого утра: настил и четверых нашли только 4–5 мая. Индивидуальных координат '
      'у них нет — одна точка на группу, и в примечании к каждому контуру это сказано прямо.']),

    ('05-labaz.png', 'Лабаз в долине Ауспии',
     '61°44′48,1″ с. ш.  59°26′58″ в. д.  ·  ≈642 м  ·  1,7 км от палатки',
     ['Сделан утром 1 февраля: в дневнике за 31 января о лабазе «и думать нечего». Береста 1,5 × 1,0 м на уровне '
      'земли, сверху дрова, доски и лапник, поверх снег. Это куча, а не яма — раскоп 2022 года нашёл настил '
      'под 2–3 см дёрна.',
      '≈55 кг продуктов шестнадцати наименований, мандолина, запасные ботинки Дятлова. Рядом воткнута пара лыж '
      'с привязанной рваной гамашей. Лабаз стоял — поэтому он плотный.']),

    ('06-camp-gear.png', 'Стоянка 31 января',
     'Призрачный слой: этого здесь в то утро уже не было',
     ['Палатку собрали и унесли днём 1 февраля. Мир показывает следующее утро, значит здесь пусто — но игроку '
      'надо видеть, что тут было. Поэтому стоянка нарисована, а не поставлена: коллизий у неё нет, сквозь неё '
      'проходишь насквозь.',
      'Девять рюкзаков на лапнике, восемь пар лыж воткнуты вдоль наветренной стенки, костёр на сырых брёвнах — '
      '«яму копать не хотелось». Внутрь палатки можно войти, только ползком.']),

    ('12-forest-edge.png', 'Граница леса',
     '≈720 м  ·  главный рубеж этой карты',
     ['Ниже — дрова и укрытие, выше — ничего. Криволесье из лиственницы, берёзы и кедра, флаговые ели с ветками '
      'только с подветренной стороны, ерник у самой границы.',
      'Деревья взяты из модели полога Meta/WRI с шагом в метр: 36 тысяч позиций и высот, породы расставлены '
      'по высотным поясам.']),
]

LEGEND = [
    ('Плотное', colors.Color(.62, .60, .42),
     'То, что стояло на месте в то утро: палатка, кедр с костром, настил в овраге, лабаз, следы. '
     'У этого есть коллизии.'),
    ('Призрачное', GHOST,
     'То, чего в то утро уже не было. Прозрачное, синевато-серое, проходимо насквозь. Чем чётче призрак, '
     'тем надёжнее известно место; бледный — наше допущение.'),
    ('Контуры с именами', colors.Color(.58, .64, .76),
     'Не тела. Место и поза, нарисованные тем, кто пришёл сюда после.'),
]


def wrap(c, text, font, size, width):
    c.setFont(font, size)
    words, lines, cur = text.split(), [], ''
    for w in words:
        t = (cur + ' ' + w).strip()
        if c.stringWidth(t, font, size) <= width:
            cur = t
        else:
            lines.append(cur); cur = w
    if cur:
        lines.append(cur)
    return lines


def flat(fn):
    """One JPEG per frame: a book of screenshots nobody can open on a phone sells nothing either."""
    os.makedirs(JPG, exist_ok=True)
    out = os.path.join(JPG, fn.replace('.png', '.jpg'))
    src = os.path.join(SHOTS, fn)
    if not os.path.exists(out) or os.path.getmtime(out) < os.path.getmtime(src):
        Image.open(src).convert('RGB').save(out, 'JPEG', quality=90, optimize=True)
    return out


def cover(c):
    """Key art, not a title page: the place first, the explanation over it."""
    img = flat(PLACES[0][0])
    iw, ih = Image.open(img).size
    scale = max(W / iw, H / ih)
    c.drawImage(img, (W - iw * scale) / 2, (H - ih * scale) / 2, iw * scale, ih * scale, mask=None)

    c.setFillColor(colors.Color(.05, .06, .09)); c.setFillAlpha(.42); c.rect(0, 0, W, H, fill=1, stroke=0)
    c.setFillAlpha(.86); c.rect(0, 0, W, 74 * mm, fill=1, stroke=0)
    c.setFillAlpha(1)

    M = 20 * mm
    c.setFillColor(PAPER); c.setFont('BodyB', 40); c.drawString(M, H - 34 * mm, '1079 · Высота')
    c.setFillColor(colors.Color(.90, .76, .48)); c.setFont('Body', 19)
    c.drawString(M, H - 47 * mm, DAY)

    y = 66 * mm
    c.setFillColor(colors.Color(.80, .83, .87)); c.setFont('Cap', 10.5)
    for line in [
        'Солнце стоит в восьми градусах над горизонтом — выше здесь в феврале оно не поднимается. Группа ушла со склона ночью.',
        'Палатка стоит разрезанная, снега на ней ещё почти нет; люди там, где их потом нашли. Игрок — тот, кто пришёл после.',
    ]:
        c.drawString(M, y, line); y -= 5.6 * mm

    y -= 5 * mm
    col_w = (W - 2 * M - 10 * mm) / 3
    for i, (name, swatch, text) in enumerate(LEGEND):
        x = M + i * (col_w + 5 * mm)
        c.setFillColor(swatch); c.rect(x, y - 1 * mm, 9 * mm, 3.4 * mm, fill=1, stroke=0)
        c.setFillColor(PAPER); c.setFont('CapB', 9.5); c.drawString(x + 11 * mm, y, name.upper())
        c.setFillColor(colors.Color(.66, .69, .74)); c.setFont('Cap', 8.6)
        yy = y - 6 * mm
        for ln in wrap(c, text, 'Cap', 8.6, col_w):
            c.drawString(x, yy, ln); yy -= 3.7 * mm

    c.setFillColor(colors.Color(.52, .55, .60)); c.setFont('Cap', 8.2)
    c.drawString(M, 14.5 * mm, 'Овраг — единственное место не из этого утра: настил и четверых нашли 4–5 мая. '
                               'Показаны на своих местах: игра про то, чтобы разобраться, а не про хронику поисков.')
    c.drawString(M, 10 * mm, 'Кадры сняла сама игра: сборка с -shots, обход мест на F3.   ·   '
                             '20 сентября 2026   ·   github.com/dsmarkin/1079-unity')
    c.showPage()


def sheet(c, n, fn, title, sub, paras):
    c.setFillColor(PAPER); c.rect(0, 0, W, H, fill=1, stroke=0)
    M = 16 * mm
    col_w = (W - 2 * M - 9 * mm) / 2

    # measure the text first; the picture takes whatever height is left, which is most of the page
    blocks = [wrap(c, p, 'Cap', 9, col_w) for p in paras]
    cap_h = max(len(b) for b in blocks) * 4.0 * mm
    head_h = 9.5 * mm + 5.8 * mm + 5.5 * mm
    avail = H - M - head_h - cap_h - 12 * mm

    img_w = W - 2 * M
    img_h = img_w * 1500.0 / 2400.0
    if img_h > avail:
        img_h = avail
        img_w = img_h * 2400.0 / 1500.0
    img_x = (W - img_w) / 2
    top = H - M
    c.drawImage(flat(fn), img_x, top - img_h, img_w, img_h, mask=None)
    c.setStrokeColor(colors.Color(.78, .78, .76)); c.setLineWidth(.6)
    c.rect(img_x, top - img_h, img_w, img_h, fill=0, stroke=1)

    y = top - img_h - 9.5 * mm
    c.setFillColor(INK); c.setFont('BodyB', 17); c.drawString(M, y, title)
    c.setFillColor(GREY); c.setFont('Cap', 9)
    c.drawRightString(W - M, y, f'{n} / {len(PLACES)}   ·   {DAY}')
    y -= 5.8 * mm
    c.setFillColor(RED); c.setFont('Cap', 9.4); c.drawString(M, y, sub)
    y -= 5.5 * mm

    c.setFillColor(INK); c.setFont('Cap', 9)
    for i, lines in enumerate(blocks):
        x = M + i * (col_w + 9 * mm)
        yy = y
        for ln in lines:
            c.drawString(x, yy, ln); yy -= 4.0 * mm
        if yy < 8 * mm:
            print('!! overflow on', fn)
    c.showPage()


def markdown():
    out = ['# Локации игрового мира', '', '**' + DAY + '.** Мир в одном состоянии: утро после.',
           'Что стояло на месте в то утро — плотное, с коллизиями. Чего уже не было — нарисовано призраком',
           'и проходимо насквозь; прозрачность несёт степень уверенности. Погибшие показаны контурами',
           'с именами, а не телами.', '',
           'Овраг — единственное место не из этого утра: настил и четверых нашли 4–5 мая.', '',
           'Кадры — `Shots/`, пишет сама игра (`1079 -shots`, обход на F3). Книжка: `python3 Tools/docs/locations.py`.', '']
    for fn, title, sub, paras in PLACES:
        out += ['## ' + title, '', '*' + sub + '*  ·  `Shots/' + fn + '`', '']
        out += [p + '\n' for p in paras]
    path = os.path.join(ROOT, 'docs', 'LOCATIONS.md')
    open(path, 'w').write('\n'.join(out))
    print('ok', path)


if '--md' in sys.argv:
    markdown()
    raise SystemExit

c = canvas.Canvas(OUT, pagesize=PAGE)
c.setTitle('1079 · Высота — локации, утро 2 февраля 1959')
c.setAuthor('1079')
cover(c)
for i, (fn, title, sub, paras) in enumerate(PLACES):
    sheet(c, i + 1, fn, title, sub, paras)
c.save()
print('ok', OUT, os.path.getsize(OUT) // 1024, 'KiB,', len(PLACES) + 1, 'pages')
