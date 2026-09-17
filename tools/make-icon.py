"""
Thunderstore icon for SkillScale. Exactly 256x256 PNG.

Preferred art: tools/icon-proposals/ts256-skillscale-b-balance.png

    python tools/make-icon.py
"""
import os
import shutil

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SOURCE = os.path.join(HERE, "icon-proposals", "ts256-skillscale-b-balance.png")
OUT = os.path.join(ROOT, "icon.png")


def main():
    if not os.path.isfile(SOURCE):
        raise SystemExit(f"missing {SOURCE}")
    shutil.copyfile(SOURCE, OUT)
    print(f"wrote {OUT} from option B")


if __name__ == "__main__":
    main()
