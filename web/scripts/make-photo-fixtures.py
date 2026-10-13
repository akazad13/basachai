"""Generates the E2E photo fixtures: room-1.jpg to room-4.jpg, and room-gps.jpg with GPS EXIF (for PHOTO-1).
Also phone-gps.jpg for the Cloudinary contract tests: phone-sized (3000 px) with GPS, so the 1,600 px incoming
limit really resizes it. Run from web/: python scripts/make-photo-fixtures.py"""
from pathlib import Path
from PIL import Image, ImageDraw

out = Path('e2e/files')
out.mkdir(parents=True, exist_ok=True)
colours = [(201, 150, 102), (102, 150, 201), (150, 201, 102), (201, 102, 150)]

def room(colour, scale=1):
    img = Image.new('RGB', (640 * scale, 480 * scale), colour)
    d = ImageDraw.Draw(img)
    d.rectangle([200 * scale, 60 * scale, 440 * scale, 240 * scale], outline=(255, 255, 255), width=8 * scale)   # a window
    d.line([0, 380 * scale, 640 * scale, 380 * scale], fill=(60, 60, 60), width=6 * scale)                      # the floor
    return img

for i, colour in enumerate(colours, start=1):
    room(colour).save(out / f'room-{i}.jpg', quality=80)

# GPS IFD: Dhamrai, roughly 23.91 N, 90.12 E.
exif = Image.Exif()
gps = {1: 'N', 2: (23.0, 54.0, 36.0), 3: 'E', 4: (90.0, 7.0, 12.0)}
exif[0x8825] = gps
room((180, 180, 180)).save(out / 'room-gps.jpg', quality=80, exif=exif.tobytes())

check = Image.open(out / 'room-gps.jpg').getexif().get_ifd(0x8825)
print('written; room-gps.jpg has GPS:', bool(check))

api_files = Path('../tests/BasaChai.Api.Tests/Files')
api_files.mkdir(parents=True, exist_ok=True)
for name in ['room-1.jpg', 'room-gps.jpg']:
    (api_files / name).write_bytes((out / name).read_bytes())
big = room((170, 160, 150)).resize((3000, 2250))
big.save(api_files / 'phone-gps.jpg', quality=70, exif=exif.tobytes())
print('phone-gps.jpg:', Image.open(api_files / 'phone-gps.jpg').size, 'GPS:', bool(Image.open(api_files / 'phone-gps.jpg').getexif().get_ifd(0x8825)))
