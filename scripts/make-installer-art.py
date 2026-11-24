"""Draws the Setup wizard sidebar (finish page): brand gradient, app icon on a tile, and the name.

Run from the repository root: python scripts/make-installer-art.py
Writes installer/wizard-<width>.png at the sizes Inno Setup picks from for each DPI.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent.parent
ICON = ROOT / 'ClipBind.png'
FONT = Path(r'C:\Windows\Fonts\seguisb.ttf')
# Taken from the icon: clipboard blue, the PDF sheet's indigo, the clip's navy.
STOPS = [(0.0, (74, 146, 255)), (0.5, (82, 66, 176)), (1.0, (19, 41, 90))]
BASE = (164, 314)  # Inno Setup modern wizard image at 100% scaling
SCALES = (1, 1.5, 2, 2.5, 3)


def color_at(t):
    for (t0, c0), (t1, c1) in zip(STOPS, STOPS[1:]):
        if t <= t1:
            f = (t - t0) / (t1 - t0)
            return tuple(round(a + (b - a) * f) for a, b in zip(c0, c1))
    return STOPS[-1][1]


def draw(width, height):
    image = Image.new('RGB', (width, height))
    pixels = ImageDraw.Draw(image)
    for y in range(height):
        pixels.line([(0, y), (width, y)], fill=color_at(y / (height - 1)))
    image = image.convert('RGBA')

    # A light rounded tile, like an app icon on the Start menu, so the colourful mark reads on the gradient.
    tile = round(width * 0.56)
    left, top = (width - tile) // 2, round(height * 0.29)
    radius = round(tile * 0.22)
    shadow = Image.new('RGBA', image.size, (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle(
        [left, top + round(tile * 0.05), left + tile, top + tile + round(tile * 0.05)], radius, fill=(0, 0, 0, 90))
    shadow = shadow.filter(ImageFilter.GaussianBlur(tile * 0.05))
    image = Image.alpha_composite(image, shadow)
    ImageDraw.Draw(image).rounded_rectangle([left, top, left + tile, top + tile], radius, fill=(248, 247, 244, 255))

    mark = round(tile * 0.74)
    icon = Image.open(ICON).convert('RGBA').resize((mark, mark), Image.LANCZOS)
    image.alpha_composite(icon, (left + (tile - mark) // 2, top + (tile - mark) // 2))

    text = ImageDraw.Draw(image)
    font = ImageFont.truetype(str(FONT), round(width * 0.135))
    name = 'ClipBind PDF'
    box = text.textbbox((0, 0), name, font=font)
    text.text(((width - (box[2] - box[0])) / 2 - box[0], top + tile + round(height * 0.07)), name, font=font, fill=(255, 255, 255))
    return image.convert('RGB')


for scale in SCALES:
    w, h = round(BASE[0] * scale), round(BASE[1] * scale)
    out = ROOT / 'installer' / f'wizard-{w}.png'
    draw(w, h).save(out, optimize=True)
    print(out.name, w, h)
