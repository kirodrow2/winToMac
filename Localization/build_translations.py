# Собирает Localization/<язык>.json из translations_*.py и проверяет их на keys.json.
# Запуск из корня проекта: python3 Localization/build_translations.py Localization/keys.json
import json, pathlib, re, sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from translations_a import EN, ZH, ES
from translations_b import HI, AR, PT
from translations_c import FR, DE, JA
from translations_extra import EXTRA

keys = json.loads(pathlib.Path(sys.argv[1]).read_text(encoding="utf-8"))
languages = {"en": EN, "zh": ZH, "es": ES, "hi": HI, "ar": AR, "pt": PT, "fr": FR, "de": DE, "ja": JA}
placeholder = re.compile(r"\{\d\}")
problems = []
for code, values in languages.items():
    if len(values) != len(keys):
        sys.exit(f"{code}: {len(values)} строк вместо {len(keys)}")
    for key, value in zip(keys, values):
        # Подстановки и переводы строк должны совпадать — иначе string.Format упадёт или текст склеится
        if sorted(placeholder.findall(key)) != sorted(placeholder.findall(value)) or key.count("\n") != value.count("\n"):
            problems.append(f"{code}: {key!r} -> {value!r}")
    table = dict(zip(keys, values))
    for key, value in EXTRA[code].items():
        if sorted(placeholder.findall(key)) != sorted(placeholder.findall(value)):
            problems.append(f"{code}: {key!r} -> {value!r}")
        table[key] = value
    out = pathlib.Path(__file__).parent / f"{code}.json"
    out.write_text(json.dumps(table, ensure_ascii=False, indent=1), encoding="utf-8")

if problems:
    sys.exit("\n".join(problems))
print("ok:", ", ".join(languages))
