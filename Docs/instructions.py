# Собирает Instructions/index.html — инструкцию winToMac на 10 языках с переключателем.
# Запуск: python3 Docs/instructions.py <папка_пакета>
import base64, html, pathlib, sys

COMBOS = ["⌘C · ⌘V · ⌘X", "⌘Z · ⌘⇧Z", "⌘A · ⌘S · ⌘F", "⌘T · ⌘W", "⌘Q", "⌘H · ⌘M", "⌘Tab", "⌘Space",
          "⌘← · ⌘→", "⌘↑ · ⌘↓", "⌘⌫", "⌥← · ⌥→ · ⌥⌫", "⌘⇧3 · ⌘⇧4", "⌘⌥Esc", "⌃⌘Q", "Caps Lock"]

T = {}

T["en"] = dict(name="English", title="winToMac — User Guide",
    intro="winToMac makes Windows look and feel like macOS: a menu bar at the top, a Dock at the bottom, Mac keyboard shortcuts and live wallpapers. It is free.",
    ui_note="The program interface is currently in Russian. Menu items are given below in quotes with a translation.",
    sections=[
    ("Requirements", ["Windows 10 (version 2004 or newer) or Windows 11, 64-bit.",
        "Live wallpapers use Microsoft Edge WebView2 Runtime — built into Windows 11; on Windows 10 it comes with updates or can be downloaded from Microsoft."]),
    ("Installation", ["Unpack the archive to any folder (for example <code>C:\\winToMac</code>) and run <b>winToMac.exe</b>. No installer is needed.",
        "If Windows shows “Windows protected your PC”, click <b>More info → Run anyway</b>. The program is not code-signed yet.",
        "Some antiviruses warn about programs that intercept the keyboard. winToMac needs this for Mac shortcuts and sends nothing over the network."]),
    ("First launch", ["The Windows taskbar hides, the menu bar and the Dock appear. Windows no longer go under them.",
        "To quit: click ⊞ in the top-left corner → «Выйти из winToMac» (Quit). The taskbar comes back.",
        "Start with Windows: ⊞ → «Настройки winToMac…» (Settings) → «Запускать вместе с Windows»."]),
    ("Dock", ["Click: launch the app, bring its window forward, or minimize it if it is already active.",
        "Right-click: list of windows, new window, pin / unpin, move, close.",
        "Drag an .exe or a shortcut onto the Dock to pin it.",
        "The first icon opens all apps with search. ☆ adds an app to favorites (= pins it to the Dock).",
        "Next to the Trash there is a “minimize all windows” button."]),
    ("Menu bar", ["⊞ — system menu: settings, live wallpapers, sleep, restart, shut down, lock.",
        "Bold text — the active app (minimize / close its window).",
        "On the right: icons of background apps (tray), keyboard layout, battery, Wi‑Fi, volume (scroll to change), clock and calendar."]),
    ("Keyboard shortcuts", ["⌘ (Command) is <b>Alt</b> on a PC keyboard and <b>Win</b> when you connect from a Mac via Moonlight/Sunshine — detected automatically on every key press.",
        "Language key: Caps Lock (short press — switch language, hold — Caps Lock). If Caps Lock is remapped in the registry (for example to F13), this is detected automatically.",
        "Your own remaps, like in PowerToys: «Настройки» (Settings) → «Переназначение клавиш».",
        "Keys are not changed in fullscreen games."]),
    ("Live wallpapers", ["⊞ → «Живые обои…» (Live wallpapers): choose a wallpaper and press «Применить» (Apply).",
        "Built-in animations in macOS and Windows 11 styles, your own videos and GIFs (looped), Apple Aerial screensavers (downloaded from Apple).",
        "Wallpapers pause when the desktop is covered by a maximized window or a game."]),
    ("Settings and removal", ["Settings are stored in <code>%APPDATA%\\winToMac</code>.",
        "To remove: quit the program, delete its folder and the folders <code>%APPDATA%\\winToMac</code>, <code>%LOCALAPPDATA%\\winToMac</code>, <code>Videos\\winToMac Wallpapers</code>.",
        "If the taskbar did not come back: start winToMac and quit via the menu, or restart Explorer: <code>taskkill /f /im explorer.exe &amp;&amp; start explorer.exe</code>."]),
    ("Support the project", ["winToMac is free. If you like it, you can support development: ⊞ → «О программе и поддержка ♥» (About and support)."]),
    ],
    keys_title="Shortcut", action_title="Action",
    actions=["Copy · Paste · Cut", "Undo · Redo", "Select all · Save · Find", "New tab · Close tab", "Quit the app",
             "Minimize the window", "Switch between apps", "Search apps", "Start / end of line", "Start / end of document",
             "Delete to the start of the line", "Move by words / delete a word", "Screenshot / screenshot of an area",
             "Task Manager", "Lock the screen", "Switch input language"])

T["ru"] = dict(name="Русский", title="winToMac — инструкция",
    intro="winToMac делает Windows похожей на macOS: строка меню сверху, док снизу, сочетания клавиш как на Mac и живые обои. Программа бесплатная.",
    ui_note="",
    sections=[
    ("Требования", ["Windows 10 (версия 2004 и новее) или Windows 11, 64-битная.",
        "Для живых обоев нужен Microsoft Edge WebView2 Runtime — в Windows 11 он встроен, в Windows 10 приходит с обновлениями или скачивается с сайта Microsoft."]),
    ("Установка", ["Распакуйте архив в любую папку (например, <code>C:\\winToMac</code>) и запустите <b>winToMac.exe</b>. Установщик не нужен.",
        "Если Windows пишет «Система Windows защитила ваш компьютер», нажмите <b>Подробнее → Выполнить в любом случае</b>. Программа пока не подписана сертификатом.",
        "Некоторые антивирусы предупреждают о программах, которые перехватывают клавиатуру. winToMac это нужно для сочетаний клавиш как на Mac; в сеть программа ничего не отправляет."]),
    ("Первый запуск", ["Панель задач Windows прячется, появляются строка меню и док. Окна больше не заходят под них.",
        "Выход: значок ⊞ в левом верхнем углу → «Выйти из winToMac». Панель задач вернётся.",
        "Автозапуск: ⊞ → «Настройки winToMac…» → «Запускать вместе с Windows»."]),
    ("Док", ["Клик — запустить программу, вывести её окно или свернуть, если оно уже активно.",
        "Правый клик — список окон, новое окно, закрепить/убрать, сдвинуть, закрыть.",
        "Перетащите .exe или ярлык на док — он закрепится.",
        "Первый значок — все приложения с поиском. ☆ добавляет в избранное (= закрепляет в доке).",
        "Рядом с корзиной — кнопка «Свернуть все окна»."]),
    ("Строка меню", ["⊞ — системное меню: настройки, живые обои, сон, перезагрузка, выключение, блокировка.",
        "Жирным — активная программа (свернуть / закрыть окно).",
        "Справа: значки фоновых программ (трей), раскладка, батарея, Wi‑Fi, громкость (колесо мыши), часы и календарь."]),
    ("Сочетания клавиш", ["⌘ (Command) — это <b>Alt</b> на клавиатуре ПК и <b>Win</b> при подключении с Mac через Moonlight/Sunshine; определяется автоматически на каждом нажатии.",
        "Клавиша смены языка — Caps Lock (коротко — сменить язык, удержание — Caps Lock). Если Caps Lock переназначен в реестре (например, на F13), это определяется само.",
        "Свои переназначения, как в PowerToys: «Настройки» → «Переназначение клавиш».",
        "В полноэкранных играх клавиши не меняются."]),
    ("Живые обои", ["⊞ → «Живые обои…»: выберите обои и нажмите «Применить».",
        "Встроенные анимации в стиле macOS и Windows 11, свои видео и GIF (по кругу), заставки Apple Aerial (скачиваются с серверов Apple).",
        "Обои ставятся на паузу, когда рабочий стол закрыт развёрнутым окном или игрой."]),
    ("Настройки и удаление", ["Настройки хранятся в <code>%APPDATA%\\winToMac</code>.",
        "Удаление: выйдите из программы, удалите её папку и папки <code>%APPDATA%\\winToMac</code>, <code>%LOCALAPPDATA%\\winToMac</code>, <code>Видео\\winToMac Wallpapers</code>.",
        "Если панель задач не вернулась: запустите winToMac и выйдите через меню или перезапустите проводник: <code>taskkill /f /im explorer.exe &amp;&amp; start explorer.exe</code>."]),
    ("Поддержать проект", ["winToMac бесплатна. Если она вам нравится, поддержите разработку: ⊞ → «О программе и поддержка ♥»."]),
    ],
    keys_title="Сочетание", action_title="Действие",
    actions=["Копировать · вставить · вырезать", "Отменить · повторить", "Выделить всё · сохранить · найти", "Новая вкладка · закрыть вкладку",
             "Закрыть программу", "Свернуть окно", "Переключение между программами", "Поиск приложений", "Начало / конец строки",
             "Начало / конец документа", "Удалить до начала строки", "Переход по словам / удалить слово", "Снимок экрана / снимок области",
             "Диспетчер задач", "Заблокировать экран", "Сменить язык ввода"])

T["zh"] = dict(name="中文", title="winToMac 使用说明",
    intro="winToMac 让 Windows 拥有 macOS 的外观和操作方式：顶部菜单栏、底部程序坞、Mac 风格快捷键和动态壁纸。本软件免费。",
    ui_note="程序界面目前为俄语。下文中的菜单名称以引号给出并附有翻译。",
    sections=[
    ("系统要求", ["Windows 10（2004 版或更高）或 Windows 11，64 位。",
        "动态壁纸需要 Microsoft Edge WebView2 Runtime——Windows 11 已内置；Windows 10 可通过更新获得或从微软官网下载。"]),
    ("安装", ["将压缩包解压到任意文件夹（例如 <code>C:\\winToMac</code>），运行 <b>winToMac.exe</b>。无需安装程序。",
        "如果出现“Windows 已保护你的电脑”，点击 <b>更多信息 → 仍要运行</b>。本程序暂未进行代码签名。",
        "部分杀毒软件会对拦截键盘的程序发出警告。winToMac 需要此功能来实现 Mac 快捷键，不会向网络发送任何数据。"]),
    ("首次启动", ["Windows 任务栏会隐藏，出现菜单栏和程序坞。窗口不会再被它们遮挡。",
        "退出：点击左上角 ⊞ → «Выйти из winToMac»（退出）。任务栏将恢复。",
        "开机自启：⊞ → «Настройки winToMac…»（设置）→ «Запускать вместе с Windows»。"]),
    ("程序坞", ["单击：启动程序、显示其窗口，若已激活则最小化。",
        "右键：窗口列表、新窗口、固定/取消固定、移动、关闭。",
        "将 .exe 或快捷方式拖到程序坞即可固定。",
        "第一个图标打开带搜索的全部应用。☆ 表示收藏（= 固定到程序坞）。",
        "废纸篓旁边有“最小化所有窗口”按钮。"]),
    ("菜单栏", ["⊞——系统菜单：设置、动态壁纸、睡眠、重启、关机、锁屏。",
        "粗体文字——当前活动程序（最小化/关闭窗口）。",
        "右侧：后台程序图标（托盘）、键盘布局、电池、Wi‑Fi、音量（滚轮调节）、时钟和日历。"]),
    ("快捷键", ["⌘（Command）在 PC 键盘上是 <b>Alt</b>，通过 Moonlight/Sunshine 从 Mac 连接时是 <b>Win</b>——每次按键自动识别。",
        "切换语言键：Caps Lock（短按切换语言，长按为大写锁定）。若在注册表中把 Caps Lock 改成了其他键（如 F13），会自动识别。",
        "自定义按键映射（类似 PowerToys）：«Настройки»（设置）→ «Переназначение клавиш»。",
        "全屏游戏中不会更改按键。"]),
    ("动态壁纸", ["⊞ → «Живые обои…»（动态壁纸）：选择壁纸后点击 «Применить»（应用）。",
        "内置 macOS 和 Windows 11 风格动画、自己的视频和 GIF（循环播放）、Apple Aerial 屏保（从 Apple 服务器下载）。",
        "当桌面被最大化窗口或游戏遮挡时，壁纸自动暂停。"]),
    ("设置与卸载", ["设置保存在 <code>%APPDATA%\\winToMac</code>。",
        "卸载：退出程序，删除程序文件夹以及 <code>%APPDATA%\\winToMac</code>、<code>%LOCALAPPDATA%\\winToMac</code>、<code>视频\\winToMac Wallpapers</code>。",
        "如果任务栏没有恢复：重新启动 winToMac 并通过菜单退出，或重启资源管理器：<code>taskkill /f /im explorer.exe &amp;&amp; start explorer.exe</code>。"]),
    ("支持项目", ["winToMac 是免费的。如果你喜欢它，可以支持开发：⊞ → «О программе и поддержка ♥»（关于与支持）。"]),
    ],
    keys_title="快捷键", action_title="功能",
    actions=["复制 · 粘贴 · 剪切", "撤销 · 重做", "全选 · 保存 · 查找", "新标签页 · 关闭标签页", "退出程序", "最小化窗口",
             "在程序间切换", "搜索应用", "行首 / 行尾", "文档开头 / 结尾", "删除到行首", "按词移动 / 删除单词",
             "截屏 / 区域截屏", "任务管理器", "锁定屏幕", "切换输入语言"])

T["es"] = dict(name="Español", title="winToMac — Guía de uso",
    intro="winToMac hace que Windows se vea y funcione como macOS: barra de menús arriba, Dock abajo, atajos de teclado de Mac y fondos de pantalla animados. Es gratuito.",
    ui_note="Por ahora la interfaz del programa está en ruso. Los elementos del menú se indican entre comillas con su traducción.",
    sections=[
    ("Requisitos", ["Windows 10 (versión 2004 o posterior) o Windows 11, de 64 bits.",
        "Los fondos animados usan Microsoft Edge WebView2 Runtime: viene integrado en Windows 11; en Windows 10 llega con las actualizaciones o se descarga de Microsoft."]),
    ("Instalación", ["Descomprime el archivo en cualquier carpeta (por ejemplo <code>C:\\winToMac</code>) y ejecuta <b>winToMac.exe</b>. No hace falta instalador.",
        "Si Windows muestra «Windows protegió su PC», pulsa <b>Más información → Ejecutar de todas formas</b>. El programa aún no está firmado digitalmente.",
        "Algunos antivirus avisan sobre programas que interceptan el teclado. winToMac lo necesita para los atajos de Mac y no envía nada por la red."]),
    ("Primer inicio", ["La barra de tareas de Windows se oculta y aparecen la barra de menús y el Dock. Las ventanas ya no quedan debajo de ellos.",
        "Para salir: pulsa ⊞ en la esquina superior izquierda → «Выйти из winToMac» (Salir). La barra de tareas vuelve.",
        "Inicio con Windows: ⊞ → «Настройки winToMac…» (Ajustes) → «Запускать вместе с Windows»."]),
    ("Dock", ["Clic: abre la aplicación, muestra su ventana o la minimiza si ya está activa.",
        "Clic derecho: lista de ventanas, nueva ventana, fijar/quitar, mover, cerrar.",
        "Arrastra un .exe o un acceso directo al Dock para fijarlo.",
        "El primer icono abre todas las aplicaciones con búsqueda. ☆ añade a favoritos (= fija en el Dock).",
        "Junto a la Papelera hay un botón para minimizar todas las ventanas."]),
    ("Barra de menús", ["⊞: menú del sistema (ajustes, fondos animados, suspender, reiniciar, apagar, bloquear).",
        "Texto en negrita: la aplicación activa (minimizar / cerrar ventana).",
        "A la derecha: iconos de aplicaciones en segundo plano (bandeja), idioma del teclado, batería, Wi‑Fi, volumen (con la rueda), reloj y calendario."]),
    ("Atajos de teclado", ["⌘ (Command) es <b>Alt</b> en un teclado de PC y <b>Win</b> cuando te conectas desde un Mac con Moonlight/Sunshine; se detecta automáticamente en cada pulsación.",
        "Tecla de idioma: Bloq Mayús (pulsación corta: cambiar idioma; mantener: mayúsculas). Si Bloq Mayús está reasignada en el registro (por ejemplo a F13), se detecta sola.",
        "Reasignaciones propias, como en PowerToys: «Настройки» (Ajustes) → «Переназначение клавиш».",
        "En juegos a pantalla completa las teclas no se modifican."]),
    ("Fondos animados", ["⊞ → «Живые обои…» (Fondos animados): elige un fondo y pulsa «Применить» (Aplicar).",
        "Animaciones al estilo macOS y Windows 11, tus propios vídeos y GIF (en bucle) y salvapantallas Apple Aerial (se descargan de Apple).",
        "El fondo se pausa cuando una ventana maximizada o un juego cubre el escritorio."]),
    ("Ajustes y desinstalación", ["Los ajustes se guardan en <code>%APPDATA%\\winToMac</code>.",
        "Para desinstalar: sal del programa y borra su carpeta y las carpetas <code>%APPDATA%\\winToMac</code>, <code>%LOCALAPPDATA%\\winToMac</code>, <code>Vídeos\\winToMac Wallpapers</code>.",
        "Si la barra de tareas no volvió: abre winToMac y sal desde el menú, o reinicia el Explorador: <code>taskkill /f /im explorer.exe &amp;&amp; start explorer.exe</code>."]),
    ("Apoya el proyecto", ["winToMac es gratuito. Si te gusta, puedes apoyar el desarrollo: ⊞ → «О программе и поддержка ♥» (Acerca de y apoyo)."]),
    ],
    keys_title="Atajo", action_title="Acción",
    actions=["Copiar · pegar · cortar", "Deshacer · rehacer", "Seleccionar todo · guardar · buscar", "Nueva pestaña · cerrar pestaña",
             "Cerrar la aplicación", "Minimizar la ventana", "Cambiar de aplicación", "Buscar aplicaciones", "Inicio / fin de línea",
             "Inicio / fin del documento", "Borrar hasta el inicio de la línea", "Moverse por palabras / borrar palabra",
             "Captura de pantalla / de un área", "Administrador de tareas", "Bloquear la pantalla", "Cambiar idioma de entrada"])

T["hi"] = dict(name="हिन्दी", title="winToMac — उपयोग गाइड",
    intro="winToMac, Windows को macOS जैसा बनाता है: ऊपर मेन्यू बार, नीचे Dock, Mac जैसे कीबोर्ड शॉर्टकट और लाइव वॉलपेपर। यह मुफ़्त है।",
    ui_note="अभी प्रोग्राम का इंटरफ़ेस रूसी भाषा में है। नीचे मेन्यू के नाम उद्धरण चिह्नों में अनुवाद के साथ दिए गए हैं।",
    sections=[
    ("आवश्यकताएँ", ["Windows 10 (संस्करण 2004 या नया) या Windows 11, 64-बिट।",
        "लाइव वॉलपेपर के लिए Microsoft Edge WebView2 Runtime चाहिए — Windows 11 में पहले से है; Windows 10 में अपडेट के साथ आता है या Microsoft से डाउनलोड करें।"]),
    ("इंस्टॉलेशन", ["आर्काइव को किसी भी फ़ोल्डर में निकालें (जैसे <code>C:\\winToMac</code>) और <b>winToMac.exe</b> चलाएँ। इंस्टॉलर की ज़रूरत नहीं।",
        "अगर Windows “Windows ने आपके PC को सुरक्षित किया” दिखाए, तो <b>अधिक जानकारी → फिर भी चलाएँ</b> दबाएँ। प्रोग्राम अभी डिजिटल रूप से साइन नहीं है।",
        "कुछ एंटीवायरस कीबोर्ड पकड़ने वाले प्रोग्राम के बारे में चेतावनी देते हैं। winToMac को Mac शॉर्टकट के लिए इसकी ज़रूरत है; यह नेटवर्क पर कुछ नहीं भेजता।"]),
    ("पहली बार चलाना", ["Windows का टास्कबार छिप जाता है, मेन्यू बार और Dock दिखते हैं। विंडो अब उनके नीचे नहीं जातीं।",
        "बाहर निकलने के लिए: ऊपर-बाएँ कोने में ⊞ → «Выйти из winToMac» (बाहर निकलें)। टास्कबार वापस आ जाएगा।",
        "Windows के साथ शुरू करना: ⊞ → «Настройки winToMac…» (सेटिंग्स) → «Запускать вместе с Windows»।"]),
    ("Dock", ["क्लिक: प्रोग्राम खोलें, उसकी विंडो सामने लाएँ, या पहले से सक्रिय हो तो छोटा करें।",
        "राइट-क्लिक: विंडो सूची, नई विंडो, पिन/अनपिन, खिसकाएँ, बंद करें।",
        ".exe या शॉर्टकट को Dock पर खींचकर पिन करें।",
        "पहला आइकन खोज के साथ सभी ऐप खोलता है। ☆ पसंदीदा में जोड़ता है (= Dock में पिन)।",
        "रीसायकल बिन के पास “सभी विंडो छोटी करें” बटन है।"]),
    ("मेन्यू बार", ["⊞ — सिस्टम मेन्यू: सेटिंग्स, लाइव वॉलपेपर, स्लीप, रीस्टार्ट, शट डाउन, लॉक।",
        "मोटे अक्षर — सक्रिय प्रोग्राम (विंडो छोटी / बंद करें)।",
        "दाईं ओर: बैकग्राउंड प्रोग्रामों के आइकन (ट्रे), कीबोर्ड भाषा, बैटरी, Wi‑Fi, आवाज़ (माउस व्हील से), घड़ी और कैलेंडर।"]),
    ("कीबोर्ड शॉर्टकट", ["⌘ (Command) PC कीबोर्ड पर <b>Alt</b> है और Moonlight/Sunshine से Mac से जुड़ने पर <b>Win</b> — हर कुंजी दबाने पर अपने-आप पहचाना जाता है।",
        "भाषा बदलने की कुंजी: Caps Lock (छोटा दबाना — भाषा बदलें, दबाए रखना — Caps Lock)। अगर रजिस्ट्री में Caps Lock बदला गया है (जैसे F13 पर), तो यह अपने-आप पहचाना जाता है।",
        "अपनी कुंजी सेटिंग, PowerToys की तरह: «Настройки» (सेटिंग्स) → «Переназначение клавиш»।",
        "फ़ुल-स्क्रीन गेम में कुंजियाँ नहीं बदली जातीं।"]),
    ("लाइव वॉलपेपर", ["⊞ → «Живые обои…» (लाइव वॉलपेपर): वॉलपेपर चुनें और «Применить» (लागू करें) दबाएँ।",
        "macOS और Windows 11 शैली के एनिमेशन, अपने वीडियो और GIF (लूप में), Apple Aerial स्क्रीनसेवर (Apple से डाउनलोड)।",
        "जब डेस्कटॉप बड़ी विंडो या गेम से ढका हो, वॉलपेपर रुक जाता है।"]),
    ("सेटिंग्स और हटाना", ["सेटिंग्स <code>%APPDATA%\\winToMac</code> में रहती हैं।",
        "हटाने के लिए: प्रोग्राम से बाहर निकलें, उसका फ़ोल्डर और <code>%APPDATA%\\winToMac</code>, <code>%LOCALAPPDATA%\\winToMac</code>, <code>Videos\\winToMac Wallpapers</code> फ़ोल्डर हटाएँ।",
        "अगर टास्कबार वापस नहीं आया: winToMac चलाकर मेन्यू से बाहर निकलें, या Explorer फिर से शुरू करें: <code>taskkill /f /im explorer.exe &amp;&amp; start explorer.exe</code>।"]),
    ("प्रोजेक्ट का समर्थन करें", ["winToMac मुफ़्त है। अगर आपको पसंद आए, तो विकास में मदद करें: ⊞ → «О программе и поддержка ♥» (परिचय और समर्थन)।"]),
    ],
    keys_title="शॉर्टकट", action_title="काम",
    actions=["कॉपी · पेस्ट · कट", "पूर्ववत · फिर से करें", "सब चुनें · सेव · खोजें", "नया टैब · टैब बंद करें", "प्रोग्राम बंद करें",
             "विंडो छोटी करें", "प्रोग्राम बदलें", "ऐप खोजें", "पंक्ति की शुरुआत / अंत", "दस्तावेज़ की शुरुआत / अंत",
             "पंक्ति की शुरुआत तक मिटाएँ", "शब्दों के अनुसार चलें / शब्द मिटाएँ", "स्क्रीनशॉट / हिस्से का स्क्रीनशॉट",
             "टास्क मैनेजर", "स्क्रीन लॉक करें", "इनपुट भाषा बदलें"])

T["ar"] = dict(name="العربية", dir="rtl", title="winToMac — دليل الاستخدام",
    intro="يجعل winToMac نظام Windows يبدو ويعمل مثل macOS: شريط قوائم في الأعلى، وDock في الأسفل، واختصارات لوحة مفاتيح Mac، وخلفيات حيّة. البرنامج مجاني.",
    ui_note="واجهة البرنامج حاليًا باللغة الروسية. أسماء عناصر القائمة مذكورة أدناه بين علامتي اقتباس مع ترجمتها.",
    sections=[
    ("المتطلبات", ["Windows 10 (الإصدار 2004 أو أحدث) أو Windows 11، بنظام 64 بت.",
        "تحتاج الخلفيات الحيّة إلى Microsoft Edge WebView2 Runtime — مدمج في Windows 11، وفي Windows 10 يأتي مع التحديثات أو يُنزَّل من موقع Microsoft."]),
    ("التثبيت", ["فكّ ضغط الأرشيف في أي مجلد (مثل <code>C:\\winToMac</code>) وشغّل <b>winToMac.exe</b>. لا حاجة إلى برنامج تثبيت.",
        "إذا ظهرت رسالة «قام Windows بحماية الكمبيوتر»، فاضغط <b>مزيد من المعلومات ← التشغيل على أي حال</b>. البرنامج غير موقّع رقميًا بعد.",
        "تحذّر بعض برامج مكافحة الفيروسات من البرامج التي تعترض لوحة المفاتيح. يحتاج winToMac ذلك لاختصارات Mac ولا يرسل أي شيء عبر الشبكة."]),
    ("التشغيل الأول", ["يختفي شريط مهام Windows ويظهر شريط القوائم والـDock. لن تمتد النوافذ تحتهما بعد الآن.",
        "للخروج: اضغط ⊞ في الزاوية العلوية اليسرى ← «Выйти из winToMac» (خروج). سيعود شريط المهام.",
        "التشغيل مع Windows: ⊞ ← «Настройки winToMac…» (الإعدادات) ← «Запускать вместе с Windows»."]),
    ("Dock", ["النقر: تشغيل البرنامج أو إظهار نافذته أو تصغيرها إذا كانت نشطة.",
        "النقر بالزر الأيمن: قائمة النوافذ، نافذة جديدة، تثبيت/إلغاء التثبيت، نقل، إغلاق.",
        "اسحب ملف ‎.exe أو اختصارًا إلى الـDock لتثبيته.",
        "الأيقونة الأولى تفتح جميع التطبيقات مع البحث. ☆ تضيف إلى المفضلة (= تثبيت في الـDock).",
        "بجانب سلة المحذوفات زر «تصغير جميع النوافذ»."]),
    ("شريط القوائم", ["⊞ — قائمة النظام: الإعدادات، الخلفيات الحيّة، السكون، إعادة التشغيل، إيقاف التشغيل، القفل.",
        "النص العريض — البرنامج النشط (تصغير / إغلاق النافذة).",
        "على اليمين: أيقونات البرامج في الخلفية، لغة لوحة المفاتيح، البطارية، Wi‑Fi، مستوى الصوت (بعجلة الفأرة)، الساعة والتقويم."]),
    ("اختصارات لوحة المفاتيح", ["⌘ (Command) هو <b>Alt</b> على لوحة مفاتيح الكمبيوتر و<b>Win</b> عند الاتصال من Mac عبر Moonlight/Sunshine — يُكتشف تلقائيًا مع كل ضغطة.",
        "مفتاح تغيير اللغة: Caps Lock (ضغطة قصيرة — تغيير اللغة، الضغط المطوّل — الأحرف الكبيرة). إذا أُعيد تعيين Caps Lock في السجل (مثلًا إلى F13) فسيُكتشف تلقائيًا.",
        "إعادة تعيين المفاتيح بنفسك مثل PowerToys: «Настройки» (الإعدادات) ← «Переназначение клавиш».",
        "لا تُغيَّر المفاتيح في الألعاب بملء الشاشة."]),
    ("الخلفيات الحيّة", ["⊞ ← «Живые обои…» (الخلفيات الحيّة): اختر خلفية واضغط «Применить» (تطبيق).",
        "رسوم متحركة بنمط macOS وWindows 11، ومقاطع فيديو وGIF خاصة بك (تتكرر)، وشاشات توقف Apple Aerial (تُنزَّل من Apple).",
        "تتوقف الخلفية مؤقتًا عندما تغطي نافذة مكبّرة أو لعبة سطح المكتب."]),
    ("الإعدادات والإزالة", ["تُحفظ الإعدادات في <code>%APPDATA%\\winToMac</code>.",
        "للإزالة: اخرج من البرنامج واحذف مجلده والمجلدات <code>%APPDATA%\\winToMac</code> و<code>%LOCALAPPDATA%\\winToMac</code> و<code>Videos\\winToMac Wallpapers</code>.",
        "إذا لم يعد شريط المهام: شغّل winToMac واخرج من القائمة، أو أعد تشغيل المستكشف: <code>taskkill /f /im explorer.exe &amp;&amp; start explorer.exe</code>."]),
    ("ادعم المشروع", ["winToMac مجاني. إذا أعجبك يمكنك دعم التطوير: ⊞ ← «О программе и поддержка ♥» (حول البرنامج والدعم)."]),
    ],
    keys_title="الاختصار", action_title="الوظيفة",
    actions=["نسخ · لصق · قص", "تراجع · إعادة", "تحديد الكل · حفظ · بحث", "علامة تبويب جديدة · إغلاق علامة التبويب", "إغلاق البرنامج",
             "تصغير النافذة", "التبديل بين البرامج", "البحث عن التطبيقات", "بداية / نهاية السطر", "بداية / نهاية المستند",
             "الحذف حتى بداية السطر", "التنقل بالكلمات / حذف كلمة", "لقطة شاشة / لقطة لجزء", "إدارة المهام", "قفل الشاشة", "تغيير لغة الإدخال"])

T["pt"] = dict(name="Português", title="winToMac — Guia de uso",
    intro="O winToMac deixa o Windows com a aparência e o uso do macOS: barra de menus no topo, Dock embaixo, atalhos de teclado do Mac e papéis de parede animados. É gratuito.",
    ui_note="Por enquanto a interface do programa está em russo. Os itens de menu aparecem entre aspas com tradução.",
    sections=[
    ("Requisitos", ["Windows 10 (versão 2004 ou mais recente) ou Windows 11, 64 bits.",
        "Os papéis de parede animados usam o Microsoft Edge WebView2 Runtime — já vem no Windows 11; no Windows 10 chega pelas atualizações ou pode ser baixado da Microsoft."]),
    ("Instalação", ["Extraia o arquivo para qualquer pasta (por exemplo <code>C:\\winToMac</code>) e execute <b>winToMac.exe</b>. Não precisa de instalador.",
        "Se o Windows mostrar “O Windows protegeu o computador”, clique em <b>Mais informações → Executar assim mesmo</b>. O programa ainda não tem assinatura digital.",
        "Alguns antivírus alertam sobre programas que interceptam o teclado. O winToMac precisa disso para os atalhos do Mac e não envia nada pela rede."]),
    ("Primeira execução", ["A barra de tarefas do Windows some e aparecem a barra de menus e o Dock. As janelas não ficam mais por baixo deles.",
        "Para sair: clique em ⊞ no canto superior esquerdo → «Выйти из winToMac» (Sair). A barra de tarefas volta.",
        "Iniciar com o Windows: ⊞ → «Настройки winToMac…» (Configurações) → «Запускать вместе с Windows»."]),
    ("Dock", ["Clique: abre o app, traz a janela para frente ou minimiza se já estiver ativa.",
        "Clique direito: lista de janelas, nova janela, fixar/desafixar, mover, fechar.",
        "Arraste um .exe ou atalho para o Dock para fixá-lo.",
        "O primeiro ícone abre todos os apps com busca. ☆ adiciona aos favoritos (= fixa no Dock).",
        "Ao lado da Lixeira há um botão para minimizar todas as janelas."]),
    ("Barra de menus", ["⊞ — menu do sistema: configurações, papéis de parede animados, suspender, reiniciar, desligar, bloquear.",
        "Texto em negrito — o app ativo (minimizar / fechar a janela).",
        "À direita: ícones de apps em segundo plano (bandeja), idioma do teclado, bateria, Wi‑Fi, volume (pela roda do mouse), relógio e calendário."]),
    ("Atalhos de teclado", ["⌘ (Command) é <b>Alt</b> num teclado de PC e <b>Win</b> quando você se conecta de um Mac pelo Moonlight/Sunshine — detectado automaticamente a cada tecla.",
        "Tecla de idioma: Caps Lock (toque curto — trocar idioma, segurar — Caps Lock). Se o Caps Lock foi remapeado no registro (por exemplo para F13), isso é detectado sozinho.",
        "Seus próprios remapeamentos, como no PowerToys: «Настройки» (Configurações) → «Переназначение клавиш».",
        "Em jogos em tela cheia as teclas não são alteradas."]),
    ("Papéis de parede animados", ["⊞ → «Живые обои…» (Papéis de parede animados): escolha um e clique em «Применить» (Aplicar).",
        "Animações nos estilos macOS e Windows 11, seus próprios vídeos e GIFs (em loop) e protetores Apple Aerial (baixados da Apple).",
        "O papel de parede pausa quando a área de trabalho está coberta por uma janela maximizada ou um jogo."]),
    ("Configurações e remoção", ["As configurações ficam em <code>%APPDATA%\\winToMac</code>.",
        "Para remover: saia do programa e apague a pasta dele e as pastas <code>%APPDATA%\\winToMac</code>, <code>%LOCALAPPDATA%\\winToMac</code>, <code>Vídeos\\winToMac Wallpapers</code>.",
        "Se a barra de tarefas não voltou: abra o winToMac e saia pelo menu, ou reinicie o Explorer: <code>taskkill /f /im explorer.exe &amp;&amp; start explorer.exe</code>."]),
    ("Apoie o projeto", ["O winToMac é gratuito. Se você gostou, apoie o desenvolvimento: ⊞ → «О программе и поддержка ♥» (Sobre e apoio)."]),
    ],
    keys_title="Atalho", action_title="Ação",
    actions=["Copiar · colar · recortar", "Desfazer · refazer", "Selecionar tudo · salvar · localizar", "Nova aba · fechar aba", "Fechar o app",
             "Minimizar a janela", "Alternar entre apps", "Buscar apps", "Início / fim da linha", "Início / fim do documento",
             "Apagar até o início da linha", "Mover por palavras / apagar palavra", "Captura de tela / de uma área",
             "Gerenciador de Tarefas", "Bloquear a tela", "Trocar idioma de entrada"])

T["fr"] = dict(name="Français", title="winToMac — Guide d’utilisation",
    intro="winToMac donne à Windows l’apparence et le fonctionnement de macOS : barre de menus en haut, Dock en bas, raccourcis clavier Mac et fonds d’écran animés. Il est gratuit.",
    ui_note="Pour l’instant, l’interface du programme est en russe. Les éléments de menu sont indiqués entre guillemets avec leur traduction.",
    sections=[
    ("Configuration requise", ["Windows 10 (version 2004 ou ultérieure) ou Windows 11, 64 bits.",
        "Les fonds animés utilisent Microsoft Edge WebView2 Runtime — intégré à Windows 11 ; sous Windows 10, il arrive avec les mises à jour ou se télécharge chez Microsoft."]),
    ("Installation", ["Décompressez l’archive dans n’importe quel dossier (par exemple <code>C:\\winToMac</code>) et lancez <b>winToMac.exe</b>. Aucun installateur n’est nécessaire.",
        "Si Windows affiche « Windows a protégé votre ordinateur », cliquez sur <b>Informations complémentaires → Exécuter quand même</b>. Le programme n’est pas encore signé.",
        "Certains antivirus avertissent au sujet des programmes qui interceptent le clavier. winToMac en a besoin pour les raccourcis Mac et n’envoie rien sur le réseau."]),
    ("Premier lancement", ["La barre des tâches de Windows se masque, la barre de menus et le Dock apparaissent. Les fenêtres ne passent plus dessous.",
        "Pour quitter : cliquez sur ⊞ en haut à gauche → «Выйти из winToMac» (Quitter). La barre des tâches revient.",
        "Démarrage avec Windows : ⊞ → «Настройки winToMac…» (Réglages) → «Запускать вместе с Windows»."]),
    ("Dock", ["Clic : lance l’application, affiche sa fenêtre ou la réduit si elle est déjà active.",
        "Clic droit : liste des fenêtres, nouvelle fenêtre, épingler/détacher, déplacer, fermer.",
        "Faites glisser un .exe ou un raccourci sur le Dock pour l’épingler.",
        "La première icône ouvre toutes les applications avec recherche. ☆ ajoute aux favoris (= épingle dans le Dock).",
        "À côté de la Corbeille, un bouton réduit toutes les fenêtres."]),
    ("Barre de menus", ["⊞ — menu système : réglages, fonds animés, veille, redémarrer, éteindre, verrouiller.",
        "Texte en gras — l’application active (réduire / fermer la fenêtre).",
        "À droite : icônes des applications en arrière-plan, disposition du clavier, batterie, Wi‑Fi, volume (molette), horloge et calendrier."]),
    ("Raccourcis clavier", ["⌘ (Commande) correspond à <b>Alt</b> sur un clavier de PC et à <b>Win</b> quand vous vous connectez depuis un Mac via Moonlight/Sunshine — détecté automatiquement à chaque touche.",
        "Touche de langue : Verr. Maj (appui court — changer de langue, appui long — majuscules). Si Verr. Maj est réassignée dans le registre (par exemple en F13), c’est détecté automatiquement.",
        "Vos propres réassignations, comme dans PowerToys : «Настройки» (Réglages) → «Переназначение клавиш».",
        "Les touches ne sont pas modifiées dans les jeux en plein écran."]),
    ("Fonds d’écran animés", ["⊞ → «Живые обои…» (Fonds animés) : choisissez un fond et cliquez sur «Применить» (Appliquer).",
        "Animations dans le style macOS et Windows 11, vos vidéos et GIF (en boucle), économiseurs Apple Aerial (téléchargés depuis Apple).",
        "Le fond se met en pause quand une fenêtre agrandie ou un jeu couvre le bureau."]),
    ("Réglages et désinstallation", ["Les réglages sont enregistrés dans <code>%APPDATA%\\winToMac</code>.",
        "Pour désinstaller : quittez le programme, supprimez son dossier et les dossiers <code>%APPDATA%\\winToMac</code>, <code>%LOCALAPPDATA%\\winToMac</code>, <code>Vidéos\\winToMac Wallpapers</code>.",
        "Si la barre des tâches n’est pas revenue : lancez winToMac et quittez via le menu, ou redémarrez l’Explorateur : <code>taskkill /f /im explorer.exe &amp;&amp; start explorer.exe</code>."]),
    ("Soutenir le projet", ["winToMac est gratuit. S’il vous plaît, vous pouvez soutenir son développement : ⊞ → «О программе и поддержка ♥» (À propos et soutien)."]),
    ],
    keys_title="Raccourci", action_title="Action",
    actions=["Copier · coller · couper", "Annuler · rétablir", "Tout sélectionner · enregistrer · rechercher", "Nouvel onglet · fermer l’onglet",
             "Quitter l’application", "Réduire la fenêtre", "Passer d’une application à l’autre", "Rechercher des applications",
             "Début / fin de ligne", "Début / fin du document", "Effacer jusqu’au début de la ligne", "Se déplacer par mots / effacer un mot",
             "Capture d’écran / d’une zone", "Gestionnaire des tâches", "Verrouiller l’écran", "Changer la langue de saisie"])

T["de"] = dict(name="Deutsch", title="winToMac — Anleitung",
    intro="winToMac lässt Windows wie macOS aussehen und funktionieren: Menüleiste oben, Dock unten, Mac-Tastenkürzel und animierte Hintergründe. Es ist kostenlos.",
    ui_note="Die Programmoberfläche ist derzeit auf Russisch. Menüpunkte sind unten in Anführungszeichen mit Übersetzung angegeben.",
    sections=[
    ("Voraussetzungen", ["Windows 10 (Version 2004 oder neuer) oder Windows 11, 64 Bit.",
        "Animierte Hintergründe nutzen die Microsoft Edge WebView2 Runtime — in Windows 11 integriert; unter Windows 10 kommt sie mit Updates oder wird bei Microsoft heruntergeladen."]),
    ("Installation", ["Entpacken Sie das Archiv in einen beliebigen Ordner (z. B. <code>C:\\winToMac</code>) und starten Sie <b>winToMac.exe</b>. Kein Installer nötig.",
        "Wenn Windows „Der Computer wurde durch Windows geschützt“ anzeigt, klicken Sie auf <b>Weitere Informationen → Trotzdem ausführen</b>. Das Programm ist noch nicht signiert.",
        "Manche Virenscanner warnen vor Programmen, die die Tastatur abfangen. winToMac braucht das für die Mac-Kürzel und sendet nichts ins Netz."]),
    ("Erster Start", ["Die Windows-Taskleiste wird ausgeblendet, Menüleiste und Dock erscheinen. Fenster rutschen nicht mehr darunter.",
        "Beenden: ⊞ oben links → «Выйти из winToMac» (Beenden). Die Taskleiste kehrt zurück.",
        "Mit Windows starten: ⊞ → «Настройки winToMac…» (Einstellungen) → «Запускать вместе с Windows»."]),
    ("Dock", ["Klick: App starten, ihr Fenster nach vorne holen oder minimieren, wenn es schon aktiv ist.",
        "Rechtsklick: Fensterliste, neues Fenster, anheften/lösen, verschieben, schließen.",
        "Ziehen Sie eine .exe oder Verknüpfung ins Dock, um sie anzuheften.",
        "Das erste Symbol öffnet alle Apps mit Suche. ☆ fügt zu Favoriten hinzu (= im Dock angeheftet).",
        "Neben dem Papierkorb gibt es eine Schaltfläche „Alle Fenster minimieren“."]),
    ("Menüleiste", ["⊞ — Systemmenü: Einstellungen, animierte Hintergründe, Energie sparen, Neustart, Herunterfahren, Sperren.",
        "Fettschrift — die aktive App (Fenster minimieren / schließen).",
        "Rechts: Symbole von Hintergrund-Apps (Infobereich), Tastaturlayout, Akku, WLAN, Lautstärke (Mausrad), Uhr und Kalender."]),
    ("Tastenkürzel", ["⌘ (Command) ist <b>Alt</b> auf einer PC-Tastatur und <b>Win</b>, wenn Sie sich per Moonlight/Sunshine von einem Mac verbinden — wird bei jedem Tastendruck automatisch erkannt.",
        "Sprachtaste: Feststelltaste (kurz — Sprache wechseln, halten — Großschreibung). Ist sie in der Registry umbelegt (z. B. auf F13), wird das automatisch erkannt.",
        "Eigene Umbelegungen wie in PowerToys: «Настройки» (Einstellungen) → «Переназначение клавиш».",
        "In Vollbildspielen werden Tasten nicht verändert."]),
    ("Animierte Hintergründe", ["⊞ → «Живые обои…» (Animierte Hintergründe): Hintergrund wählen und «Применить» (Übernehmen) drücken.",
        "Animationen im macOS- und Windows-11-Stil, eigene Videos und GIFs (in Schleife), Apple-Aerial-Bildschirmschoner (von Apple heruntergeladen).",
        "Der Hintergrund pausiert, wenn ein maximiertes Fenster oder Spiel den Desktop verdeckt."]),
    ("Einstellungen und Entfernen", ["Die Einstellungen liegen in <code>%APPDATA%\\winToMac</code>.",
        "Entfernen: Programm beenden, seinen Ordner sowie <code>%APPDATA%\\winToMac</code>, <code>%LOCALAPPDATA%\\winToMac</code>, <code>Videos\\winToMac Wallpapers</code> löschen.",
        "Falls die Taskleiste nicht zurückkam: winToMac starten und über das Menü beenden oder den Explorer neu starten: <code>taskkill /f /im explorer.exe &amp;&amp; start explorer.exe</code>."]),
    ("Projekt unterstützen", ["winToMac ist kostenlos. Wenn es Ihnen gefällt, unterstützen Sie die Entwicklung: ⊞ → «О программе и поддержка ♥» (Über und Unterstützung)."]),
    ],
    keys_title="Kürzel", action_title="Aktion",
    actions=["Kopieren · Einfügen · Ausschneiden", "Rückgängig · Wiederholen", "Alles auswählen · Speichern · Suchen", "Neuer Tab · Tab schließen",
             "App beenden", "Fenster minimieren", "Zwischen Apps wechseln", "Apps suchen", "Zeilenanfang / -ende", "Dokumentanfang / -ende",
             "Bis zum Zeilenanfang löschen", "Wortweise bewegen / Wort löschen", "Bildschirmfoto / Bereichsfoto",
             "Task-Manager", "Bildschirm sperren", "Eingabesprache wechseln"])

T["ja"] = dict(name="日本語", title="winToMac 使い方ガイド",
    intro="winToMac は Windows を macOS のような見た目と操作にします。上部のメニューバー、下部の Dock、Mac のキーボードショートカット、ライブ壁紙。無料です。",
    ui_note="現在、プログラムの画面はロシア語です。メニュー項目は「」内のロシア語と訳で示しています。",
    sections=[
    ("動作環境", ["Windows 10（バージョン 2004 以降）または Windows 11、64 ビット。",
        "ライブ壁紙には Microsoft Edge WebView2 Runtime が必要です。Windows 11 には標準搭載、Windows 10 では更新プログラムで入るか Microsoft から入手できます。"]),
    ("インストール", ["アーカイブを任意のフォルダー（例: <code>C:\\winToMac</code>）に展開し、<b>winToMac.exe</b> を実行します。インストーラーは不要です。",
        "「Windows によって PC が保護されました」と表示されたら、<b>詳細情報 → 実行</b> をクリックします。プログラムはまだコード署名されていません。",
        "一部のウイルス対策ソフトはキーボードを監視するプログラムを警告します。winToMac は Mac のショートカットのためにこれを使い、ネットワークへは何も送信しません。"]),
    ("初回起動", ["Windows のタスクバーが隠れ、メニューバーと Dock が表示されます。ウィンドウはその下に潜り込まなくなります。",
        "終了：左上の ⊞ →「Выйти из winToMac」（終了）。タスクバーが元に戻ります。",
        "Windows と同時に起動：⊞ →「Настройки winToMac…」（設定）→「Запускать вместе с Windows」。"]),
    ("Dock", ["クリック：アプリを起動、ウィンドウを前面に表示、すでにアクティブなら最小化。",
        "右クリック：ウィンドウ一覧、新しいウィンドウ、固定/固定解除、移動、閉じる。",
        ".exe やショートカットを Dock にドラッグすると固定されます。",
        "最初のアイコンは検索付きの全アプリ一覧を開きます。☆ でお気に入りに追加（= Dock に固定）。",
        "ゴミ箱の隣に「すべてのウィンドウを最小化」ボタンがあります。"]),
    ("メニューバー", ["⊞ — システムメニュー：設定、ライブ壁紙、スリープ、再起動、シャットダウン、ロック。",
        "太字 — アクティブなアプリ（ウィンドウを最小化 / 閉じる）。",
        "右側：バックグラウンドアプリのアイコン（トレイ）、キーボード言語、バッテリー、Wi‑Fi、音量（マウスホイール）、時計とカレンダー。"]),
    ("キーボードショートカット", ["⌘（Command）は PC キーボードでは <b>Alt</b>、Moonlight/Sunshine で Mac から接続したときは <b>Win</b> です。キーを押すたびに自動で判別します。",
        "言語切り替えキー：Caps Lock（短く押す — 言語切り替え、長押し — Caps Lock）。レジストリで Caps Lock を別のキー（例: F13）に割り当てている場合も自動で判別します。",
        "PowerToys のような独自のキー割り当て：「Настройки」（設定）→「Переназначение клавиш」。",
        "全画面のゲームではキーは変更されません。"]),
    ("ライブ壁紙", ["⊞ →「Живые обои…」（ライブ壁紙）：壁紙を選んで「Применить」（適用）を押します。",
        "macOS と Windows 11 風のアニメーション、自分の動画や GIF（ループ再生）、Apple Aerial スクリーンセーバー（Apple からダウンロード）。",
        "最大化したウィンドウやゲームでデスクトップが隠れているときは一時停止します。"]),
    ("設定とアンインストール", ["設定は <code>%APPDATA%\\winToMac</code> に保存されます。",
        "削除するには：プログラムを終了し、そのフォルダーと <code>%APPDATA%\\winToMac</code>、<code>%LOCALAPPDATA%\\winToMac</code>、<code>ビデオ\\winToMac Wallpapers</code> を削除します。",
        "タスクバーが戻らない場合：winToMac を起動してメニューから終了するか、エクスプローラーを再起動します：<code>taskkill /f /im explorer.exe &amp;&amp; start explorer.exe</code>。"]),
    ("プロジェクトを支援", ["winToMac は無料です。気に入ったら開発を支援してください：⊞ →「О программе и поддержка ♥」（情報とサポート）。"]),
    ],
    keys_title="ショートカット", action_title="動作",
    actions=["コピー · 貼り付け · 切り取り", "元に戻す · やり直し", "すべて選択 · 保存 · 検索", "新しいタブ · タブを閉じる",
             "アプリを終了", "ウィンドウを最小化", "アプリの切り替え", "アプリを検索", "行頭 / 行末", "文書の先頭 / 末尾",
             "行頭まで削除", "単語単位で移動 / 単語を削除", "スクリーンショット / 範囲のスクリーンショット",
             "タスク マネージャー", "画面をロック", "入力言語を切り替え"])

ORDER = ["en", "ru", "zh", "es", "hi", "ar", "pt", "fr", "de", "ja"]

# Интерфейс теперь переведён: подпись «как в программе» берётся из Localization/<язык>.json
LANGUAGE_NOTE = {
    "en": "The interface language can be changed at any time: ⊞ → «Язык интерфейса» (Interface language).",
    "ru": "Язык интерфейса можно сменить в любой момент: ⊞ → «Язык интерфейса».",
    "zh": "界面语言可随时更改：⊞ → «Язык интерфейса»。",
    "es": "El idioma de la interfaz se puede cambiar en cualquier momento: ⊞ → «Язык интерфейса».",
    "hi": "इंटरफ़ेस भाषा कभी भी बदली जा सकती है: ⊞ → «Язык интерфейса»।",
    "ar": "يمكن تغيير لغة الواجهة في أي وقت: ⊞ ← «Язык интерфейса».",
    "pt": "O idioma da interface pode ser alterado a qualquer momento: ⊞ → «Язык интерфейса».",
    "fr": "La langue de l’interface peut être changée à tout moment : ⊞ → «Язык интерфейса».",
    "de": "Die Sprache der Oberfläche lässt sich jederzeit ändern: ⊞ → «Язык интерфейса».",
    "ja": "表示言語はいつでも変更できます：⊞ →「Язык интерфейса」。",
}
CAPS_NOTE = {
    "en": "Caps Lock modes and a system-level remap of Caps Lock to F13 (works even without winToMac, needs administrator rights and a restart): «Настройки» → «Смена языка».",
    "ru": "Режимы Caps Lock и системное переназначение Caps Lock в F13 (работает даже без winToMac, нужны права администратора и перезагрузка): «Настройки» → «Смена языка».",
    "zh": "Caps Lock 模式以及在系统层面把 Caps Lock 改为 F13（即使不运行 winToMac 也有效，需要管理员权限并重启）：«Настройки» → «Смена языка»。",
    "es": "Modos de Bloq Mayús y reasignación de Bloq Mayús a F13 a nivel del sistema (funciona incluso sin winToMac; requiere permisos de administrador y reiniciar): «Настройки» → «Смена языка».",
    "hi": "Caps Lock के मोड और सिस्टम स्तर पर Caps Lock को F13 बनाना (winToMac के बिना भी काम करता है, एडमिनिस्ट्रेटर अधिकार और रीस्टार्ट चाहिए): «Настройки» → «Смена языка»।",
    "ar": "أوضاع Caps Lock وإعادة تعيينه إلى F13 على مستوى النظام (يعمل حتى بدون winToMac، ويتطلب صلاحيات المسؤول وإعادة التشغيل): «Настройки» ← «Смена языка».",
    "pt": "Modos do Caps Lock e remapeamento do Caps Lock para F13 no sistema (funciona até sem o winToMac; requer direitos de administrador e reiniciar): «Настройки» → «Смена языка».",
    "fr": "Modes de Verr. Maj et réassignation système de Verr. Maj en F13 (fonctionne même sans winToMac ; nécessite des droits d’administrateur et un redémarrage) : «Настройки» → «Смена языка».",
    "de": "Modi der Feststelltaste und systemweite Umbelegung auf F13 (funktioniert auch ohne winToMac, erfordert Administratorrechte und Neustart): «Настройки» → «Смена языка».",
    "ja": "Caps Lock のモードと、システムレベルで Caps Lock を F13 にする設定（winToMac なしでも有効、管理者権限と再起動が必要）：「Настройки」→「Смена языка」。",
}

# Русские подписи в тексте инструкции → ключ перевода интерфейса
LABEL_KEYS = {
    "Выйти из winToMac": "Выйти из winToMac", "Настройки winToMac…": "Настройки winToMac…",
    "Запускать вместе с Windows": "Запускать вместе с Windows", "Настройки": "Настройки winToMac",
    "Переназначение клавиш": "Переназначение клавиш", "Живые обои…": "Живые обои…", "Применить": "Применить",
    "О программе и поддержка ♥": "О программе и поддержка ♥", "Язык интерфейса": "Язык интерфейса", "Смена языка": "Смена языка",
}


def localize_labels(code, text, tables):
    # «Русская подпись» и её перевод в скобках заменяются подписью из перевода интерфейса
    import re
    table = tables.get(code, {})
    def replace(match):
        russian = match.group(2)
        key = LABEL_KEYS.get(russian)
        if key is None:
            return match.group(0)
        label = table.get(key, russian)
        return f"{match.group(1)}{label}{match.group(3)}"
    pattern = re.compile(r"([«「])([^»」]+)([»」])(?:\s*(?:\([^)]*\)|（[^）]*）))?")
    return pattern.sub(replace, text)


def section_html(code, data, tables):
    parts = [f'<article class="doc" lang="{code}" dir="{data.get("dir", "ltr")}" hidden>',
             f'<h1>{html.escape(data["title"])}</h1>', f'<p class="lead">{data["intro"]}</p>',
             f'<p class="note">{localize_labels(code, LANGUAGE_NOTE[code], tables)}</p>']
    for index, (heading, items) in enumerate(data["sections"]):
        items = list(items) + ([CAPS_NOTE[code]] if index == 5 else [])
        parts.append(f"<h2>{html.escape(heading)}</h2><ul>" + "".join(f"<li>{localize_labels(code, i, tables)}</li>" for i in items) + "</ul>")
        if heading == data["sections"][5][0]:
            rows = "".join(f"<tr><td><kbd>{html.escape(c)}</kbd></td><td>{html.escape(a)}</td></tr>"
                           for c, a in zip(COMBOS, data["actions"]))
            parts.append(f'<table><thead><tr><th>{data["keys_title"]}</th><th>{data["action_title"]}</th></tr></thead><tbody>{rows}</tbody></table>')
    parts.append("</article>")
    return "".join(parts)


def build(out_dir, icon_path):
    assert all(len(T[c]["actions"]) == len(COMBOS) for c in ORDER)
    import json
    localization = pathlib.Path(__file__).resolve().parent.parent / "Localization"
    tables = {c: json.loads((localization / f"{c}.json").read_text(encoding="utf-8")) for c in ORDER if (localization / f"{c}.json").exists()}
    icon = base64.b64encode(pathlib.Path(icon_path).read_bytes()).decode()
    buttons = "".join(f'<button data-lang="{c}">{html.escape(T[c]["name"])}</button>' for c in ORDER)
    docs = "".join(section_html(c, T[c], tables) for c in ORDER)
    page = f"""<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>winToMac — Instructions</title>
<style>
:root{{--bg:#f5f5f7;--card:#fff;--fg:#1d1d1f;--muted:#6e6e73;--line:#d2d2d7;--accent:#0a64d8;--kbd:#eef0f4}}
@media (prefers-color-scheme:dark){{:root{{--bg:#161618;--card:#1f1f22;--fg:#f5f5f7;--muted:#a1a1a6;--line:#3a3a3e;--accent:#4c9aff;--kbd:#2c2c30}}}}
*{{box-sizing:border-box}}
body{{margin:0;background:var(--bg);color:var(--fg);font:16px/1.6 "Segoe UI Variable Text","Segoe UI",system-ui,sans-serif}}
header{{position:sticky;top:0;background:color-mix(in srgb,var(--bg) 88%,transparent);backdrop-filter:blur(12px);border-bottom:1px solid var(--line);z-index:1}}
.bar{{max-width:880px;margin:0 auto;padding:12px 16px;display:flex;gap:12px;align-items:center;flex-wrap:wrap}}
.brand{{display:flex;align-items:center;gap:10px;font-weight:600;font-size:18px;margin-right:auto}}
.brand img{{width:36px;height:36px}}
.langs{{display:flex;flex-wrap:wrap;gap:6px}}
.langs button{{border:1px solid var(--line);background:var(--card);color:var(--fg);border-radius:999px;padding:4px 12px;font:inherit;font-size:14px;cursor:pointer}}
.langs button[aria-pressed=true]{{background:var(--accent);border-color:var(--accent);color:#fff}}
main{{max-width:880px;margin:0 auto;padding:24px 16px 64px}}
.doc{{background:var(--card);border:1px solid var(--line);border-radius:16px;padding:8px 28px 24px}}
h1{{font-size:30px;line-height:1.25;margin:20px 0 8px}}
h2{{font-size:20px;margin:28px 0 8px}}
.lead{{font-size:18px;color:var(--muted)}}
.note{{background:var(--kbd);border-radius:10px;padding:10px 14px;font-size:15px}}
ul{{padding-inline-start:22px}} li{{margin:6px 0}}
code{{background:var(--kbd);border-radius:6px;padding:1px 6px;font-size:14px;word-break:break-all}}
table{{border-collapse:collapse;width:100%;margin-top:8px;font-size:15px}}
th,td{{text-align:start;padding:8px 10px;border-bottom:1px solid var(--line)}}
th{{color:var(--muted);font-weight:600}}
kbd{{background:var(--kbd);border:1px solid var(--line);border-radius:6px;padding:2px 8px;font:inherit;white-space:nowrap}}
@media (max-width:600px){{.doc{{padding:4px 16px 16px}} h1{{font-size:24px}}}}
</style></head><body>
<header><div class="bar"><div class="brand"><img src="data:image/png;base64,{icon}" alt="">winToMac</div><nav class="langs">{buttons}</nav></div></header>
<main>{docs}</main>
<script>
const docs=[...document.querySelectorAll('.doc')], btns=[...document.querySelectorAll('.langs button')];
function show(code){{
  if(!docs.some(d=>d.lang===code)) code='en';
  docs.forEach(d=>d.hidden=d.lang!==code);
  btns.forEach(b=>b.setAttribute('aria-pressed',b.dataset.lang===code));
  document.documentElement.lang=code;
  try{{localStorage.setItem('winToMac.lang',code)}}catch(e){{}}
}}
btns.forEach(b=>b.addEventListener('click',()=>show(b.dataset.lang)));
let saved=null; try{{saved=localStorage.getItem('winToMac.lang')}}catch(e){{}}
show(location.hash.slice(1) || saved || (navigator.language||'en').slice(0,2).toLowerCase());
</script></body></html>"""
    out = pathlib.Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    (out / "index.html").write_text(page, encoding="utf-8")


if __name__ == "__main__":
    build(sys.argv[1], sys.argv[2])
    print("ok")
