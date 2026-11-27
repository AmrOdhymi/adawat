# حالة التطوير

> آخر تحديث: 2026-11-26 — المرحلة 2 اعتُمدت (Paper & Ink وGraphite Pro)، والمرحلة 3 نُفِّذت بمراجعة ساكنة مع دفعتَي إصلاحات، والمرحلة 4 أُعيد تعريفها إلى «عارض نصوص منسّق أصلي» ثم أُكملت بالدفعات A–E (تجميد اللقطة، استعادة النوافذ، العارض بتبويبات، PDF بحد 256MB، وقارئ LaTeX موسّع)؛ وتلاها تحديث 2026-11-26 لتمرير PDF الأسرع، زر Open واحد، وطبقة نص/بحث PDF، ثم تثبيت الإغلاق التلقائي حول حوارات الحفظ وتكافؤ أزرار حوار الحافظة، وتأكيد الحذف بخياريه المستقلين في الإعدادات، وإصلاح حذف عنصر الحافظة بعد التأكيد — كل ذلك بانتظار بناء واختبار المستخدم.
> المرجع: `Docs/PROJECT_AUDIT.md` للتفاصيل، و`Docs/DEVELOPMENT_ROADMAP.md` لترتيب المراحل.

---

## 1. الحالة العامة

| البند | القيمة |
|---|---|
| المرحلة الحالية | **المرحلة 4 — عارض النصوص المنسّق/PDF (نُفِّذت، مراجعة ساكنة + تحديث 2026-11-26، بانتظار بناء واختبار المستخدم)** |
| المرحلة النشطة التالية | المرحلة 5 (بعد تحقق المستخدم من الثيمين والعارض) |
| آخر التزام كودي | `0102919 phase 1-improve windows behavior` |
| تعديلات الكود في هذه المرحلة | `Theme.cs`, `UI.cs`, `Settings.cs`, `SettingsWindow.cs`, `Native.cs`, `Program.cs`, `Snapshots.cs`, `MarkdownViewer.cs`, `Documents/*` + نوافذ العجلة/HUD/Float/الحافظة/المفكرة/QR/اللقطة/التعليقات |
| حالة البناء/التشغيل | المرحلة 1 بُنيت واختُبرت من المستخدم؛ لا بناء/تشغيل ضمن هذه الجلسة |

### إحصاء المتطلبات

| الحالة | العدد |
|---|---|
| `COMPLETED` | 4 |
| `IMPLEMENTED` (بانتظار اختبار المستخدم) | 9 |
| `CANCELLED` (بقرار المستخدم) | 2 |
| `PARTIALLY IMPLEMENTED` | 9 |
| `BUGGY` | 2 |
| `NOT IMPLEMENTED` | 10 |
| `NEEDS USER RUNTIME TEST` | 1 |
| `NEEDS CODE INVESTIGATION` | 0 |
| `DEFERRED` | 1 |
| **الإجمالي** | **38** |

> `COMPLETED` يعني متطلباً نُفِّذ وتحقق منه المستخدم؛ بقية المتطلبات إضافات/إصلاحات فوق تطبيق قائم. الميزات القائمة نفسها (التحويلات، العجلة، Float، OCR) تعمل تاريخياً لكنها خارج نطاق هذه القائمة.

---

## 2. جدول المتطلبات

| المعرف | المتطلب                        | الحالة                    | المرحلة                       | ملاحظة مختصرة                                           |
| ------ | ------------------------------ | ------------------------- | ----------------------------- | ------------------------------------------------------- |
| A-01   | ترجمة النصوص المحددة           | `NOT IMPLEMENTED`         | 9                             | يحتاج عقد مزود + مزود Online                            |
| A-02   | ترجمة محتوى الشاشة             | `PARTIALLY IMPLEMENTED`   | 10                            | OCR/Overlay موجودان، الترجمة ناقصة                      |
| A-03   | نافذة الترجمة                  | `NOT IMPLEMENTED`         | 9                             | تعتمد على A-01                                          |
| A-04   | وميض فتح العجلة                | `COMPLETED`               | 1                             | تحقق المستخدم 2026-11-17                                |
| B-01   | Network Monitor                | `NOT IMPLEMENTED`         | 8                             | خدمة + نافذة لحظية                                      |
| B-02   | الاستهلاك التراكمي             | `NOT IMPLEMENTED`         | 8                             | تخزين يومي/أسبوعي/شهري                                  |
| B-03   | إعداد Network Adapter          | `NOT IMPLEMENTED`         | 8                             | يعتمد على B-01                                          |
| C-01   | معاينة الحافظة                 | `BUGGY`                   | 5                             | النقر ينسخ بدلاً من المعاينة                            |
| C-02   | Context Menu للمفكرة + العنوان | `BUGGY`                   | 5                             | العنوان أُصلح بـ`FieldRow`؛ قائمة السياق ما زالت مطلوبة |
| C-03   | إرسال Bluetooth                | `DEFERRED`                | —                             | أُجّل بقرار المستخدم وأُخرج من الخطة الحالية            |
| D-01   | خطأ Window Is Closing          | `COMPLETED`               | 1                             | تحقق المستخدم 2026-11-17                                |
| D-02   | منع أخطاء الإغلاق المتكررة     | `COMPLETED`               | 1                             | تحقق المستخدم 2026-11-17                                |
| E-01   | التحميل التدريجي               | `NOT IMPLEMENTED`         | 5                             | أول 20 + Load More                                      |
| F-01   | Document Viewer مشترك          | `IMPLEMENTED`             | 4                             | معاينة بسيطة للحافظة/المفكرة + عارض `DocumentViewer` بتبويبات — بانتظار الاختبار |
| F-02   | صيغ الملفات المدعومة           | `IMPLEMENTED`             | 4                             | md/txt/html/rtf/docx/doc/odt/epub + tex/latex + pdf (صفحات) |
| F-03   | إمكانات Markdown               | `IMPLEMENTED`             | 4                             | GFM عبر Markdig AST                                      |
| F-04   | LaTeX والمعادلات               | `IMPLEMENTED`             | 4                             | `TexFlow` أشمل + `MathText` إلى Unicode                  |
| F-05   | عارض PDF                       | `IMPLEMENTED`             | 4 (دفعة D+تحديث 2026-11-26)   | صفحات صور عند الطلب + حد ذاكرة 256MB + طبقة نص/بحث/تحديد لملفات PDF النصية |
| F-06   | المحتوى المضمن                 | `IMPLEMENTED`             | 4                             | صور محلية داخل مجلد المستند فقط                          |
| F-07   | العرض المتقدم                  | `CANCELLED`               | —                             | Mermaid/Highlight/Copy Code ملغاة                       |
| F-08   | البنية التقنية المقترحة        | `CANCELLED`               | —                             | لا WebView2؛ WPF أصلي                                    |
| F-09   | قيود القراءة فقط               | `IMPLEMENTED`             | 4                             | تكبير/RTL/نسخ/تصدير؛ لا تحرير؛ العارض لا يمسّ الأصل     |
| F-10   | فتح الملفات من العجلة          | `IMPLEMENTED`             | 4 (دفعة C)                    | `action.open` + عجلتا Alt وCtrl+Space                   |
| F-11   | الأمان والأداء                 | `IMPLEMENTED`             | 4                             | لا JS ولا شبكة؛ مسارات مقيّدة                            |
| F-12   | الثيمات                        | `PARTIALLY IMPLEMENTED`   | 3 ثم 4                        | Light/Dark للثيمين المعتمدين؛ لا System mode            |
| G-01   | خمسة تصاميم                    | `PARTIALLY IMPLEMENTED`   | 2                             | عرض تفاعلي جاهز في `Docs/design-preview`                |
| G-02   | اختيار التصميم                 | `COMPLETED`               | 2                             | اعتُمد Paper & Ink وGraphite Pro                        |
| G-03   | تطبيق Design System            | `PARTIALLY IMPLEMENTED`   | 3 (أساس) ثم 12 (كامل)         | رموز الثيم والأنماط الضمنية مطبقة                       |
| H-01   | طول نافذة الإعدادات            | `NEEDS USER RUNTIME TEST` | 5 (نُفِّذ ضمن دفعة المرحلة 3) | تقسيم أفقي بتبويبات وتمرير؛ بانتظار تحقق المستخدم       |
| H-02   | دمج إعدادات الميزات            | `PARTIALLY IMPLEMENTED`   | 3، 5، 8، 9، 11، 12            | الثيم ووضعه مضافان؛ الشبكة والترجمة ناقصتان             |
| I-01   | بنية محرك الترجمة              | `NOT IMPLEMENTED`         | 9                             | واجهة مزود موحدة                                        |
| I-02   | إدارة حزم الترجمة              | `NOT IMPLEMENTED`         | 11                            | مؤجلة بقرار المستخدم                                    |
| I-03   | التقنيات المرشحة               | `NOT IMPLEMENTED`         | 11                            | دراسة ONNX/MarianMT                                     |
| I-04   | فصل محرك الترجمة               | `NOT IMPLEMENTED`         | 9                             | Online/Offline خلف واجهة                                |
| R-01   | جودة الكود                     | `PARTIALLY IMPLEMENTED`   | مستمر + 12                    | تقليل التكرار                                           |
| R-02   | إعادة استخدام المكونات         | `PARTIALLY IMPLEMENTED`   | مستمر + 12                    | مكونات مشتركة جديدة                                     |
| R-03   | تنظيم المجلدات                 | `PARTIALLY IMPLEMENTED`   | 12                            | قرار مطلوب                                              |
| R-04   | إعادة الهيكلة التدريجية        | `PARTIALLY IMPLEMENTED`   | مستمر                         | قاعدة إلزامية لكل مرحلة                                 |

---

## 3. حالة كل مرحلة

| المرحلة | الحالة | ملاحظة |
|---|---|---|
| 0 — التدقيق | ✅ مكتملة | بانتظار الموافقة |
| 1 — استقرار النوافذ والعجلة | ✅ مكتملة ومتحققة | D-01/D-02/A-04 (تحقق المستخدم 2026-11-17) |
| 2 — التصاميم الخمسة | ✅ معتمدة | اعتُمد Paper & Ink وGraphite Pro (2026-11-18) |
| 3 — أساس الثيم والتصميم | 🟡 نُفِّذت | G-03 أساس + F-12 أساس + إعداد الثيم — بانتظار بناء/اختبار المستخدم |
| 4 — عرض النصوص المنسّق (أصلي) | 🟡 نُفِّذت + دفعات A–E | F-01…F-06/F-09/F-10/F-11 — بانتظار بناء/اختبار المستخدم |
| 5 — الحافظة والمفكرة والإعدادات | ⏸ لم تبدأ | C-01/C-02/E-01/H-01 |
| 6 — LaTeX الكامل وPDF والعرض المتقدم | ⛔ جزئياً ملغاة | PDF نُفِّذ داخل العارض (دفعة D)؛ KaTeX/Mermaid/Highlight/WebView2 ملغاة |
| 7 — فتح المستندات من العجلة | ✅ نُفِّذت ضمن دفعة C | F-10 عبر `action.open` وعجلتي Alt وCtrl+Space |
| 8 — مراقبة الشبكة | ⏸ لم تبدأ | B-01/B-02/B-03 |
| 9 — الترجمة Online والنافذة | ⏸ لم تبدأ | I-01/I-04/A-01/A-03 |
| 10 — ترجمة الشاشة | ⏸ لم تبدأ | A-02 |
| 11 — الترجمة دون إنترنت | ⏸ لم تبدأ | I-02/I-03 (قرار) |
| 12 — التكامل النهائي والتنظيف | ⏸ لم تبدأ | G-03 كامل + R-01…R-04 |

---

## 3.1 ما تم في المرحلة 1 (متحقق)

| الملف | التغيير |
|---|---|
| `WindowLifecycle.cs` (جديد) | تتبع `Closing/Closed`، `IsClosingOrClosed`، `CloseOnce`، `HideOnce`، `CanAutoClose`، `ActivateIfOpen` |
| `UI.cs` | `ToolWindow` يتتبع دورة الحياة، زر X وEscape يستخدمان `CloseOnce`، و`Present` لا يعيد العرض أثناء الإغلاق |
| `ClipboardWindow` / `NotepadWindow` / `QuickLauncherWindow` / `WelcomeWindow` / `SettingsWindow` | `Present` آمن، `Deactivated` محصّن، وإعادة الفتح بعد `Closed` |
| `QrResultWindow` / `ScreenshotPreviewWindow` | `CloseOnce` في الأزرار وEscape و`Deactivated` |
| `WheelWindow` / `QuickWheelWindow` | `EnsureHandle → Place → Draw → Show`، عرض واحد، وحماية `HideWheel` |
| `Floats` / `Hud` / `Runner` / `ToolWindows` | عدم لمس نافذة قيد الإغلاق، وعدم تحديث HUD بعد بدء خروج التطبيق |
| `Windows/CompressWindow` / `Windows/ImageWindows` | إعادة الفتح بعد `Closed` بدل الإغلاق ثم الفتح الفوري |

**نتيجة التحقق:** أكّد المستخدم نجاح المرحلة 1 بتاريخ 2026-11-17، واعتُمدت D-01/D-02/A-04 كمكتملة.

---

## 3.2 ما تم في المرحلة 2 (عرض التصاميم)

| الملف | المحتوى |
|---|---|
| `Docs/design-preview/index.html` | صفحة عرض واحدة، شريط علوي لتبديل التصاميم، Light/Dark، وكل الأسطح |
| `Docs/design-preview/preview.css` | رموز التصميم للخمسة (Light/Dark) + تخطيط متجاوب + حركة خفيفة |
| `Docs/design-preview/preview.js` | التبديل، الحفظ المحلي، عجلة SVG، QR، HUD، البحث، الاختصارات |

**التصاميم:** Fluent Calm، Paper & Ink، Soft Mint، Graphite Pro، Violet Studio.

**الأسطح المعروضة:** العجلة السريعة، العجلة المدمجة، HUD، الحافظة، المفكرة، الإعدادات، Float والقائمة، نتيجة QR، معاينة اللقطة، عناصر التحكم، الألوان، الخطوط، الأيقونات، وأسطح مستقبلية (عارض الملفات، الترجمة، مراقبة الشبكة).

**ملاحظة:** اعتُمد الثيمان **Paper & Ink** و**Graphite Pro**، ونُفِّذا في المرحلة 3. بقيت بقية التصاميم في صفحة العرض كمرجع تصميمي فقط.

---

## 3.3 ما تم في المرحلة 3 (تنفيذ الثيمين)

| الملف | التغيير |
|---|---|
| `Theme.cs` | لوحات Paper/Graphite لكل وضع، فرش قابلة للتعديل، خطوط، أنصاف أقطار، `Apply`/`ApplyFromSettings` |
| `Settings.cs` | `AppTheme`، `AppThemeMode`، `Theme`، `ThemeMode` (الافتراضي Paper + Light) |
| `SettingsWindow.cs` | قسم Appearance، تبديل فوري، إعادة بناء الجسم/التذييل، تحديث النوافذ المفتوحة |
| `UI.cs` | أنماط ضمنية للعناصر القياسية، `InstallThemeResources`، `RefreshOpenWindows`، `ToolWindow.RefreshTheme` |
| `Native.cs` | `DarkTitleBar` |
| `Program.cs` / `Snapshots.cs` | تطبيق الثيم والأنماط قبل إنشاء النوافذ |
| العجلة/HUD/Float/الحافظة/المفكرة/Welcome/QR/اللقطة/TextOverlay/FloatDialogs/Markdown/QuickLauncher/PDF/الصوت/الفيديو/الصور/التعليقات | استبدال الأسطح والزوايا الثابتة برموز الثيم |
| `Docs/design-preview/preview.css` / `preview.js` | Graphite Pro بزوايا 6–20 px مع الحفاظ على ألوانه |

**حدود النطاق:** Paper & Ink لم يتغير بصرياً. لا System mode. لا تغيير على Bluetooth/C-03. لم يُنفَّذ Build/تشغيل/اختبار واجهة.

---

## 3.4 دفعة إصلاحات ما بعد البناء (2026-11-18)

| الملف | التغيير |
|---|---|
| `Theme.cs` | `Publish()` لتسجيل الفرش والخطوط كمصادر حيّة بعد كل `Apply` |
| `UI.cs` | تحويل الأنماط/القوالب إلى `DynamicResource`/`SetResourceReference`، سكرول بار موحّد بـXAML، توسيط النص في TextBox، `FieldRow`، خط ديناميكي |
| `SettingsWindow.cs` | نافذة 820 px بتقسيم أفقي وستة تبويبات + تمرير، تحديث فوري، وإزالة أسر `Func` الذي سبّب CS0165 |
| `QuickLauncherWindow.cs` | حقول ممتدة وأزرار Browse أسفل حقل الهدف |
| `NotepadWindow.cs` | عنوان المفكرة بـ`FieldRow` |
| `Wheel/WheelWindow.cs` / `QuickWheelWindow.cs` | عرض/خط تلقائي للعجلة مع حشوة والتفاف |
| `NotepadWindow.cs` / `ClipboardWindow.cs` / `Windows/QrWindow.cs` / `QrResultWindow.cs` | `VerticalContentAlignment=Top` للمحررات متعددة الأسطر |
| `Windows/MediaKit.cs` | حقل الزمن يحتفظ بارتفاع 24 عبر `MinHeight` محلي |
| `Docs/CHANGELOG.md` / `TEST_REPORT.md` | توثيق الدفعة والتحققات |

**السبب الجذري للتبديل غير الفوري:** تخزين فرش `Theme` داخل `Style`/`ControlTemplate` يجعل WPF يجمّدها (`Freezable`)، فمحاولة تعديل لونها عند تبديل الثيم ترمي «read-only state». الحل: الإشارة إليها بمفاتيح موارد ديناميكية بدل تخزين النسخة.

**السبب الجذري لتوسيط النص:** حامل النص في `TextBox` يتمدد لارتفاع الصندوق، فيبقى النص أعلى. الحل: مشغّل قالب يضبط `VerticalAlignment=Center` على `PART_ContentHost` عندما يكون `VerticalContentAlignment=Center`؛ المحررات متعددة الأسطر تختار `Top` صراحةً فلا تتوسّط.

**أنصاف الأقطار:** تتحدث فوراً للنوافذ التي يعاد بناؤها (Settings)، ولأي نافذة تُفتح بعد التبديل؛ النوافذ الأخرى المفتوحة تحتفظ بزواياها حتى إعادة الفتح. لا يلزم إعادة تشغيل التطبيق.

**حدود النطاق:** لا Build/تشغيل/اختبار واجهة. Paper & Ink لم يتغير بصرياً. لا تغيير على Bluetooth/C-03.

---

## 3.5 دفعة إصلاحات التبديل الحيّ والتباعد (2026-11-21)

| الملف | التغيير |
|---|---|
| `Theme.cs` | `Publish()` ينشر نسخاً مجمّدة من الفرش بدل الفرش الحيّة؛ `Set` يستبدل أي فرش مجمّد بدل رمي استثناء |
| `UI.cs` | هوامش `Row`/`FieldRow`، حشوة وهامش `Segmented`، وحشوة/ارتفاع `Button` |
| `SettingsWindow.cs` | تباعد عناصر التنقل وعناوين الأقسام |
| `ClipboardWindow.cs` | تباعد صف البحث والفلاتر والعدّاد وأزرار البطاقات |
| `Windows/VideoWindows.cs` / `ImageWindows.cs` / `AnnotateWindow.cs` | هوامش أزرار الصفوف المدمجة |

**السبب الجذري للتبديل:** `Application.Resources` يجمّد أي `Freezable` يُضاف إليه (`ResourceDictionary.SealValue`). كان `Theme.Publish()` يضع الفرش الحيّة نفسها في الموارد، فتُجمّد، ثم يرمي `Apply` عند محاولة تغيير لونها ويتوقف قبل `Publish`/`RefreshOpenWindows`. الحل: نشر نسخ مجمّدة فقط، وإبقاء الفرش الحيّة خارج الموارد.

**حدود النطاق:** لا Build/تشغيل/اختبار واجهة. لا تغيير على Bluetooth/C-03 أو دورة حياة Phase 1. Paper & Ink بقي بصرياً كما في العرض.

---

## 3.6 دفعة إصلاحات القوائم والزوايا الحيّة (2026-11-22)

| الملف | التغيير |
|---|---|
| `Theme.cs` | نشر `Theme.RadiusSmall/Medium/Large/Xl/Card/Control/Pill` في `Publish()` |
| `UI.cs` | `Menu()`/`MenuEntry()`، امتدادا `Radius` للـ`Border` والقالب، و`InvalidateTree` داخل `RefreshOpenWindows`، وتحويل 11 موضع زوايا |
| `Tray.cs` | فتح القائمة يدوياً بالزرين الأيسر والأيمن، إزالة `ContextMenu` من الأيقونة، وإعادة البناء عند `Theme.Changed` |
| `Floats.cs` / `Windows/PdfWindows.cs` | القوائم عبر المصنع الجديد، وزوايا حيّة |
| `ScreenCapture.cs` | تحديد شفاف (`Brushes.Transparent`) + زاوية شريط الأدوات |
| `Native.cs` | `RoundCorners(window, small)` وثوابت DWM (`ROUNDSMALL`/`ROUND`) |
| `ClipboardWindow`/`FloatDialogs`/`TextOverlayWindow`/`ScreenshotPreviewWindow`/`QrWindow`/`QrResultWindow`/`NotepadWindow`/`WelcomeWindow`/`MarkdownViewer`/`AnnotateWindow`/`QuickLauncherWindow`/`Hud`/`MediaKit`/`AudioWindows` | تحويل بقية مواضع `Theme.*Radius` إلى مراجع موارد، وإعادة رسم الزوايا اليدوية |

**السبب الجذري للقوائم:** كانت قائمة الشريط تُبنى مرة واحدة وتُسلَّم إلى `H.NotifyIcon`، وكان الزر الأيمن يمر عبر مساره الداخلي. صار البناء والفتح بيد التطبيق، مع مرجع مورد لكل نص وأيقونة، فأي تبديل لاحق للثيم يحدّث اللون في مكانه أو يعيد بناء القائمة.

**حدود النطاق:** لا Build/تشغيل/اختبار واجهة. لا تغيير على Bluetooth/C-03 أو دورة حياة Phase 1. زوايا إطار النافذة الخارجي تبقى تقريباً أصلياً من DWM (4px/8px) لأن DWM لا يدعم 4/18px.

---

## 3.7 المرحلة 4 — عارض النصوص المنسّق (2026-11-23)

> **قرار المستخدم (2026-11-22):** إلغاء «محرك عرض المستندات» (WebView2) وإلغاء «LaTeX وPDF والعرض المتقدم». المطلوب: أن تتعرف واجهة المعاينة الحالية على Markdown وLaTeX فتعرضهما منسّقين، وأن تقبل سحب ملف مدعوم وفتحه في نفس الواجهة.
>
> **تحديث 2026-11-25:** الجدول التالي يصف حالة 2026-11-23 قبل دفعات A–E. الآن `MarkdownViewer` معاينة بسيطة فقط، و`DocumentViewer` هو عارض التبويبات/السحب/Open، وPDF وLaTeX الموسّع في 3.8.

| الملف | التغيير |
|---|---|
| `Documents/FlowKit.cs` (جديد) | صفحة `FlowDocument` بفرش الثيم، كتل كود، خطوط فاصلة، روابط، ملاحظات، صور مع تقييد المسار بمجلد المستند |
| `Documents/MathText.cs` (جديد) | عزل `$…$`/`$$…$$`/`\(…\)`/`\[…\]` قبل Markdown، ثم رسم Unicode: رموز، كسور متراكبة، أسس/لواحق، جذور، خطوط، أنماط نصية |
| `Documents/MarkdownFlow.cs` (جديد) | Markdig AST ← `FlowDocument`: GFM كامل + `LooksLikeMarkdown` لكشف النص المنسّق |
| `Documents/DocFlow.cs` (جديد) | `Doc` (docx/doc/odt/rtf/html/epub) ← نفس المظهر |
| `MarkdownViewer.cs` | نافذة معاينة للقراءة فقط: `OpenPreview` و`OpenFile`، سحب وإفلات، تكبير، RTL/LTR، نسخ، تصدير نسخة، فتح ملف |
| `UI.cs` | `ToolWindow.Header` لتحديث عنوان النافذة عند تحميل ملف مسحوب |

**قرارات:** لا WebView2 ولا JavaScript ولا شبكة ولا تعقيم HTML (لا HTML يُنفَّذ). لا KaTeX: LaTeX يُحوَّل إلى Unicode تقريبياً. لا حزم NuGet جديدة؛ Markdig موجود مسبقاً.

**حدود معروفة:** LaTeX ليس تنضيداً حقيقياً؛ الأمر المجهول يبقى نصاً. الملفات الكبيرة قد تحتاج وقتاً داخل `FlowDocument`. `.doc` يعتمد على Word/COM وقد يفشل برسالة واضحة.

**حدود النطاق:** لا Build/تشغيل/اختبار واجهة. لا تغيير على Bluetooth/C-03 أو دورة حياة Phase 1.

---

## 3.8 دفعات المرحلة 4 النهائية A–E (2026-11-25)

> **الطلب:** تجميد الشاشة أثناء اللقطة، إصلاح استعادة النوافذ المصغّرة، فصل معاينة الحافظة/المفكرة عن عارض بعجلات متعدد التبويبات، دعم PDF داخل العارض بحد ذاكرة 256MB، وقارئ LaTeX أشمل — كله بلا محرك ويب ولا حزم جديدة.

| الملف | التغيير |
|---|---|
| `ScreenCapture.cs` | الدفعة A: صورة اللقطة المجمّدة أول عنصر خلف القناع، `Stretch.Fill` وجودة عالية وبلا تفاعل، وتُعاد مطابعتها عند تغيّر حجم النافذة |
| `WindowLifecycle.cs` | الدفعة B: `ActivateIfOpen` يعيد المصغّرة، و`BringToFront` يعرض المخفية/يعيد المصغّرة/يركّز |
| `UI.cs` | الدفعة B: `ToolWindow.Present` يستخدم `BringToFront` بدل `Activate` وحده |
| `NotepadWindow.cs` / `MarkdownViewer.cs` | `ShowInTaskbar = false` للمعاينة فقط؛ محرر الملاحظة صار مثل حوار تعديل الحافظة (`Owner` بلا `ShowInTaskbar = false`) بعد أن ثبت أن الفرق الوحيد هو ما يمنع إصلاح التصغير في المفكرة |
| `UI.cs` | متابعة 2026-11-26: `ToolWindow` يفصل المالك على مستوى Win32 في `Closing` (`SetWindowLongPtr` + `GWL_HWNDPARENT = 0`) لأن `Owner = null` يعيد أبوة نوافذ `ShowInTaskbar = false` إلى نافذة WPF المخفية؛ ويخفي النافذة قبل الفصل تفادي وميض زر شريط المهام، ويستعيد سلسلة المالكين كشبكة أمان |
| `WindowLifecycle.cs` / `Native.cs` | متابعة 2026-11-26: `Native.DetachOwner` و`Native.ShowWindow`، و`WindowLifecycle.KeepRestored` يعيد قائمتي المفكرة والحافظة إلى `Normal` إن صغّرهما النظام كأثر جانبي |
| `ClipboardWindow.cs` / `NotepadWindow.cs` | متابعة 2026-11-26: `Highlight` يمر على كل البطاقات فيزيل تضليل البطاقة السابقة فوراً بعد `RefreshList`، بدل الاعتماد على مرجع بطاقة قد يصبح قديماً |
| `WindowLifecycle.cs` | متابعة 2026-11-26: `HoldAutoClose` يمنع إغلاق القوائم أثناء حوار حفظ ملف أصلي (`SaveFileDialog`) عبر عدّاد على خاصية مرفقة يفحصه `CanAutoClose`، ويُطبَّق حول `ShowDialog` فقط |
| `ClipboardWindow.cs` | متابعة 2026-11-26: حوار `Edit Clipboard Entry` يضيف `Copy` و`Export...` مثل حوار المفكرة؛ `ExportText` يصدّر النص إلى `.md`/`.txt`؛ قائمة السياق النصية تضيف `Export...`؛ حفظ الصورة وتصدير النص يعلّقان الإغلاق التلقائي حول حوار الحفظ؛ واتساع حوار التعديل صار 620 مثل حوار المفكرة ليتسع للأزرار الستة |
| `NotepadWindow.cs` / `MarkdownViewer.cs` | متابعة 2026-11-26: تصدير الملاحظة وتصدير المعاينة يعلّقان الإغلاق التلقائي حول `SaveFileDialog` فلا تُغلق القائمة أثناء الحفظ |
| `ConfirmDialog.cs` (جديد) | متابعة 2026-11-26: `ConfirmDialog.Ask` حوار تأكيد مملوك ومودال (Cancel/Delete، و`Escape` إلغاء)، ويعيد `false` إلا عند الضغط على زر التأكيد، ويعلّق الإغلاق التلقائي للقائمة أثناء عرضه |
| `Settings.cs` / `SettingsWindow.cs` | متابعة 2026-11-26: خياران جديدان `ConfirmClipboardDelete` و`ConfirmNoteDelete` افتراضهما `true`، يظهران في تبويب General قسم Deleting، ويشملهما Restore Defaults |
| `ClipboardWindow.cs` / `NotepadWindow.cs` | متابعة 2026-11-26: حذف العنصر وClear History في الحافظة وحذف الملاحظة يسألان عبر `ConfirmDialog` عندما يكون الخيار المقابل مفعّلاً، وإلا يُحذف مباشرة. وفي الحافظة تُعاد قراءة `entries` بعد الحوار ويُزال العنصر بالمعرّف قبل حذف ملف الصورة، ثم `Save` و`RefreshList`، لأن `Activated` يعيد تحميل القائمة بعد إغلاق الحوار |
| `UI.cs` | متابعة 2026-11-27: هامش 8px يسار سهم `ComboBox` في القالب المشترك، فانفصل النص عن السهم في كل القوائم المنسدلة ومنها الإعدادات |
| `NotepadWindow.cs` / `ClipboardWindow.cs` | متابعة 2026-11-27: حوارا الإضافة يعرضان `Cancel`/`RTL / LTR`/`Import...`/`Preview...` وزر `Add` فقط (حوار الحافظة بعرض 620)، وبقي `Copy`/`Export` في حوارَي التعديل؛ `Import...` يقرأ `.txt`/`.md` ويستبدل المحتوى ويعلّق الإغلاق التلقائي حول المنتقي؛ وأُضيف تنقّل `Up/Down` بين البطاقات الظاهرة بلا التفاف مع `BringIntoView`، و`Enter` يفتح المحدد (تعديل/تفاصيل/معاينة صورة) مع ترك Enter للأزرار |
| `Documents/DocumentViewer.cs` (جديد) | الدفعة C: نافذة 980×720 قابلة للتحجيم، تبويبات أعلى، Open، سحب وإفلات، تبويب لكل ملف، إغلاق تبويب، `Ctrl+W/Tab/O`، وحفظ الحالة عند الإغلاق. متابعة 2026-11-26: إزالة Export، بحث موحد لـPDF وFlowDocument، وتمرير FlowDocument أكثر سلاسة. |
| `Documents/PreviewTabsStore.cs` (جديد) | حفظ المسارات والترتيب والنشط ونسبة التمرير والتكبير في `preview-tabs.json` (20 تبويباً)، وتجاهل المفقود |
| `MarkdownViewer.cs` | الدفعة C: عاد معاينة بسيطة فقط للحافظة/المفكرة؛ بلا Open أو سحب ملفات أخرى، مع التكبير/RTL/النسخ/التصدير. متابعة 2026-11-26: يستخدم كمعاينة مؤقتة من قوائم الحافظة/المفكرة («Open in viewer») دون إضافة تبويب دائم في `DocumentViewer`؛ أُضيف له شريط بحث (`Ctrl+F/F3/Enter/Shift+Enter/Escape`) وتمرير أكثر سلاسة عبر `PreviewMouseWheel`، وكشف LaTeX (`TexFlow.LooksLikeLatex`) وHTML (`HtmlReader`+`DocFlow`) قبل Markdown/النص العادي. |
| `Adawat.Core/Catalog.cs` / `FileKind.cs` | `action.open` لكل ملف قابل للعرض، و`Viewable` = المستندات + `pdf` + `tex/latex` |
| `Windows/Catalog.cs` / `Wheel/QuickWheelWindow.cs` / `SettingsWindow.cs` / `Settings.cs` | تسجيل `documents`، وإضافته إلى عجلة Ctrl+Space وقائمة أدوات العجلة، وترحيل `QuickToolsVersion` |
| `Adawat.Core/Pdf/PdfOps.cs` | `PageSizes` و`RenderPagePng` |
| `Documents/PdfView.cs` (جديد) | الدفعة D: مكان لكل صفحة، رسم الصفحات الظاهرة عند الطلب (6 لكل مسح)، فتح/إغلاق الملف لكل صفحة، LRU بحد 256MB، تكبير 40–250%، نسبة تمرير محفوظة. وبعد الدفعة E: `PageCount/CurrentPage/GoToPage`، حدث `PageChanged`، و`FitWidthZoom/FitPageZoom` |
| `Documents/DocumentViewer.cs` | الدفعة D: تبويب PDF، تكبير/تمرير خاصان به، تفريغ ذاكرة تبويبات PDF غير النشطة، تعطيل النسخ/التصدير له. وبعد الدفعة E: شريط «Page» مع `Ctrl+G`، زرّا Fit/Page، مفاتيح PageUp/PageDown/Home/End، زر Open in app، ورسالة واضحة لملف PDF المحمي |
| `Documents/TexFlow.cs` (جديد) | الدفعة E: قارئ LaTeX موسّع (أقسام، قوائم، اقتباسات، كود، جداول، صور، روابط، حواشٍ، نظريات، تنسيق، رموز، رياضيات عبر `MathText`) |
| `Documents/MathText.cs` | إصلاح `\\[2mm]`، وحماية `$` داخل النص الحرفي |
| `NotepadWindow.cs` / `ClipboardWindow.cs` | متابعة 2026-11-27: تحميل النص الطويل على دفعات 5000 حرف (أول دفعة فوراً ثم دفعة عند كل وصول إلى أسفل المربع عبر `AppendNextChunk` + `ScrollChanged` + مؤقت 120ms)، مع `IsUndoEnabled = false` أثناء التحميل، و`CurrentContent`/`CurrentText` تُعيد النص كاملاً للحفظ/النسخ/التصدير/المعاينة، و`FlushPending` عند التحرير قبل اكتمال التحميل، وتصفير الحالة عند الاستيراد. البحث في قائمتي الملاحظات والحافظة على النص كامل. |
| `Documents/FlowKit.cs` / `MarkdownFlow.cs` / `DocFlow.cs` / `MarkdownViewer.cs` / `DocumentViewer.cs` | متابعة 2026-11-27: `IsOptimalParagraphEnabled = false` في `NewDocument` (تخطيط 1075 فقرة من ~3.7s إلى ~0.2s)، وبناء غير متزامن `FromMarkdownAsync`/`FromPlainAsync`/`FromDocAsync` مع `yieldForUi`، ومعاينة تبني بعد الظهور بشريط تقدم، و`KeepViews = 6` في العارض |

**قرارات:** معاينة الحافظة/المفكرة تبقى بسيطة بلا Open أو سحب ملفات أخرى؛ العارض متعدد التبويبات هو وحده الذي يقبل Open والسحب. التبويبات تُحفظ ولا تُمحى عند الإغلاق. PDF صور صفحات بلا تحديد نص، وذاكرة LRU 256MB لكل تبويب PDF نشط فقط. LaTeX ليس تنضيداً حقيقياً؛ الأمر المجهول يبقى بنص وسيطه.

**حدود معروفة:** لا Build/تشغيل/اختبار واجهة. لا WebView2 ولا حزم جديدة. التكبير في PDF يعيد الرسم من الملف عند الحاجة، وقد يظهر فراغ قصير أثناء التمرير السريع. تحديد/بحث PDF يعتمد على النص المستخرج من PdfPig؛ ملفات PDF الممسوحة ضوئياً بلا نص لن تدعم التحديد/البحث حتى إضافة OCR. الملفات الضخمة قد تحتاج وقتاً داخل `FlowDocument`؛ محررا الملاحظة والحافظة يعرضان النص على دفعات 5000 حرف بدل تحميله كاملاً في تمريرة واحدة. `.doc` يعتمد على Word/COM.

**حدود النطاق:** لا تغيير على Bluetooth/C-03 أو دورة حياة Phase 1.

---

## 4. ما يحتاج إلى تأكيد من المستخدم

1. ~~الموافقة على ترتيب المراحل~~ — تمت.
2. قرار بشأن نطاق الترجمة دون إنترنت (المرحلة 11): تنفيذ أم تأجيل.
3. ~~اختيار التصميم من العرض~~ — تم: Paper & Ink وGraphite Pro؛ المطلوب الآن بناء واختبار الثيمين فورياً، مع التحقق من السكرول بار والمربعات النصية والتبويبات وعجلة الخيارات.
4. ~~تأكيد سلوك الإغلاق التلقائي عند فقدان التركيز~~ — تم: يبقى الإغلاق التلقائي مع تحصينه.
5. ~~تأكيد حزمة عرض المستندات~~ — أُلغيت الحزمة الثقيلة؛ العارض WPF أصلي بلا أصول خارجية.
6. ~~اختبار Bluetooth~~ — أُجّل بقرار المستخدم وأُخرج من الخطة الحالية.
7. **دفعات A–E + تحديث 2026-11-26:** تجميد الشاشة أثناء اللقطة، عودة النوافذ المصغّرة، التبويبات من العجلتين، PDF بحد 256MB، قارئ LaTeX، ثم تمرير PDF أسرع/زر Open واحد/طبقة نص وبحث PDF، وكشف LaTeX/HTML في المعاينة، ومنع تصغير القائمة عند إغلاق التابع، وتثبيت الإغلاق التلقائي حول حوارات الحفظ، وتكافؤ أزرار حوار الحافظة، وتأكيد الحذف بخياريه المستقلين، وإصلاح حذف عنصر الحافظة بعد التأكيد. **ومتابعة 2026-11-27:** فاصل سهم القوائم، وأزرار حوارَي الإضافة مع الاستيراد، والتنقل بالأسهم و`Enter` في الحافظة والمفكرة — بانتظار البناء ثم T-14…T-27.

> لا يُعتبر أي متطلب مكتملاً إلا بعد تعديل الكود ومراجعته، ثم بناء واختبار المستخدم.
