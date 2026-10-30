# Строки, добавленные после первой волны переводов: русский ключ -> перевод по языкам.
K1 = "💡 Не хватает какой-то функции? Напишите мне в Telegram {0} — добавлю её в новой версии."
K2 = "Написать в Telegram"
K3 = "💡 Предложить функцию (Telegram)"

EXTRA = {
    "en": {K1: "💡 Missing a feature? Message me on Telegram {0} and I'll add it in the next version.", K2: "Message on Telegram", K3: "💡 Suggest a feature (Telegram)"},
    "zh": {K1: "💡 缺少某个功能？请在 Telegram {0} 上联系我，我会在新版本中加入。", K2: "在 Telegram 上留言", K3: "💡 建议新功能（Telegram）"},
    "es": {K1: "💡 ¿Te falta alguna función? Escríbeme en Telegram {0} y la añadiré en la próxima versión.", K2: "Escribir en Telegram", K3: "💡 Sugerir una función (Telegram)"},
    "hi": {K1: "💡 कोई सुविधा नहीं मिल रही? मुझे Telegram {0} पर लिखें — मैं इसे अगले संस्करण में जोड़ दूँगा।", K2: "Telegram पर लिखें", K3: "💡 सुविधा सुझाएँ (Telegram)"},
    "ar": {K1: "💡 هل تفتقد ميزة ما؟ راسلني على Telegram {0} وسأضيفها في الإصدار القادم.", K2: "راسلني على Telegram", K3: "💡 اقترح ميزة (Telegram)"},
    "pt": {K1: "💡 Falta alguma função? Escreva-me no Telegram {0} e eu a adiciono na próxima versão.", K2: "Escrever no Telegram", K3: "💡 Sugerir uma função (Telegram)"},
    "fr": {K1: "💡 Il vous manque une fonction ? Écrivez-moi sur Telegram {0}, je l'ajouterai dans la prochaine version.", K2: "Écrire sur Telegram", K3: "💡 Proposer une fonction (Telegram)"},
    "de": {K1: "💡 Fehlt dir eine Funktion? Schreib mir auf Telegram {0} – ich baue sie in die nächste Version ein.", K2: "Auf Telegram schreiben", K3: "💡 Funktion vorschlagen (Telegram)"},
    "ja": {K1: "💡 欲しい機能がありますか？Telegram {0} でご連絡ください。次のバージョンで追加します。", K2: "Telegram で連絡する", K3: "💡 機能を提案する（Telegram）"},
}

# Обновления (1.3)
U = [
    "Не удалось проверить обновления: {0}", "У вас последняя версия {0}.", "Обновление {0}",
    "Доступна новая версия {0} (у вас {1})",
    "Обновление скачается ({0} МБ) и установится само, программа перезапустится.",
    "Пропустить эту версию", "Позже", "Обновить", "Скачивание…", "Запуск новой версии…",
    "Не удалось обновить: {0}", "Проверить обновления…", "Проверять обновления автоматически",
]
UPDATES = {
    "en": ["Couldn't check for updates: {0}", "You have the latest version {0}.", "{0} update",
           "New version {0} is available (you have {1})",
           "The update ({0} MB) will download and install by itself; the app will restart.",
           "Skip this version", "Later", "Update", "Downloading…", "Starting the new version…",
           "Couldn't update: {0}", "Check for updates…", "Check for updates automatically"],
    "zh": ["无法检查更新：{0}", "您使用的已是最新版本 {0}。", "{0} 更新",
           "有新版本 {0} 可用（当前为 {1}）",
           "更新（{0} MB）将自动下载并安装，程序会重新启动。",
           "跳过此版本", "稍后", "更新", "正在下载…", "正在启动新版本…",
           "更新失败：{0}", "检查更新…", "自动检查更新"],
    "es": ["No se pudo buscar actualizaciones: {0}", "Tienes la última versión {0}.", "Actualización de {0}",
           "Hay una nueva versión {0} (tienes la {1})",
           "La actualización ({0} MB) se descargará e instalará sola; la app se reiniciará.",
           "Omitir esta versión", "Más tarde", "Actualizar", "Descargando…", "Iniciando la nueva versión…",
           "No se pudo actualizar: {0}", "Buscar actualizaciones…", "Buscar actualizaciones automáticamente"],
    "hi": ["अपडेट जाँच नहीं सके: {0}", "आपके पास नवीनतम संस्करण {0} है।", "{0} अपडेट",
           "नया संस्करण {0} उपलब्ध है (आपके पास {1} है)",
           "अपडेट ({0} MB) अपने आप डाउनलोड और इंस्टॉल होगा; ऐप फिर से शुरू होगा।",
           "यह संस्करण छोड़ें", "बाद में", "अपडेट करें", "डाउनलोड हो रहा है…", "नया संस्करण शुरू हो रहा है…",
           "अपडेट नहीं हो सका: {0}", "अपडेट जाँचें…", "अपडेट अपने आप जाँचें"],
    "ar": ["تعذّر التحقق من التحديثات: {0}", "لديك أحدث إصدار {0}.", "تحديث {0}",
           "يتوفر إصدار جديد {0} (لديك {1})",
           "سيتم تنزيل التحديث ({0} ميغابايت) وتثبيته تلقائيًا، وسيُعاد تشغيل البرنامج.",
           "تخطي هذا الإصدار", "لاحقًا", "تحديث", "جارٍ التنزيل…", "جارٍ تشغيل الإصدار الجديد…",
           "تعذّر التحديث: {0}", "التحقق من التحديثات…", "التحقق من التحديثات تلقائيًا"],
    "pt": ["Não foi possível verificar atualizações: {0}", "Você tem a versão mais recente {0}.", "Atualização do {0}",
           "Nova versão {0} disponível (você tem a {1})",
           "A atualização ({0} MB) será baixada e instalada sozinha; o app será reiniciado.",
           "Pular esta versão", "Mais tarde", "Atualizar", "Baixando…", "Iniciando a nova versão…",
           "Não foi possível atualizar: {0}", "Verificar atualizações…", "Verificar atualizações automaticamente"],
    "fr": ["Impossible de vérifier les mises à jour : {0}", "Vous avez la dernière version {0}.", "Mise à jour de {0}",
           "Nouvelle version {0} disponible (vous avez la {1})",
           "La mise à jour ({0} Mo) se téléchargera et s’installera seule ; l’application redémarrera.",
           "Ignorer cette version", "Plus tard", "Mettre à jour", "Téléchargement…", "Démarrage de la nouvelle version…",
           "Échec de la mise à jour : {0}", "Rechercher des mises à jour…", "Rechercher les mises à jour automatiquement"],
    "de": ["Updates konnten nicht geprüft werden: {0}", "Du hast die neueste Version {0}.", "{0}-Update",
           "Neue Version {0} verfügbar (du hast {1})",
           "Das Update ({0} MB) wird automatisch geladen und installiert; die App startet neu.",
           "Diese Version überspringen", "Später", "Aktualisieren", "Wird heruntergeladen…", "Neue Version wird gestartet…",
           "Update fehlgeschlagen: {0}", "Nach Updates suchen…", "Automatisch nach Updates suchen"],
    "ja": ["アップデートを確認できませんでした: {0}", "最新バージョン {0} をお使いです。", "{0} のアップデート",
           "新しいバージョン {0} があります（現在 {1}）",
           "アップデート（{0} MB）は自動でダウンロード・インストールされ、アプリが再起動します。",
           "このバージョンをスキップ", "後で", "アップデート", "ダウンロード中…", "新しいバージョンを起動中…",
           "アップデートできませんでした: {0}", "アップデートを確認…", "自動的にアップデートを確認"],
}
for _code, _values in UPDATES.items():
    assert len(_values) == len(U), _code
    EXTRA[_code].update(zip(U, _values))
