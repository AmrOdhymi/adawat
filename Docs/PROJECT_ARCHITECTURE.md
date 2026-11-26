# البنية المعمارية للمشروع

> وثيقة وصفية للبنية الحالية بعد التحقق من الكود عند الالتزام `6e9749d`.
> ما يظهر تحت «إضافات مقترحة» هو تخطيط مستقبلي غير منفذ.

---

## 1. هيكل المستودع

```
adawat/
├─ Docs/                         كل التوثيق
│  ├─ MASTER_DEVELOPMENT_SPECIFICATION.md
│  ├─ PROJECT_ANALYSIS.md        تحليل سابق (مرجع تاريخي)
│  ├─ PROJECT_AUDIT.md
│  ├─ PROJECT_ARCHITECTURE.md
│  ├─ DEVELOPMENT_STATUS.md
│  ├─ DEVELOPMENT_ROADMAP.md
│  ├─ CHANGELOG.md
│  └─ TEST_REPORT.md
├─ windows/
│  ├─ Adawat.slnx
│  ├─ Directory.Build.props
│  ├─ GlobalUsings.cs
│  └─ src/
│     ├─ Adawat.Core/            منطق الأعمال والتحويلات (WPF FlowDocument)
│     ├─ Adawat.App/             تطبيق Tray وكل النوافذ
│     └─ Adawat.Cli/             واجهة أوامر وفحوص تشخيصية
└─ assets/                       الأيقونات
```

### 1.1 إعدادات البناء المشتركة

- `TargetFramework`: `net10.0-windows10.0.22621.0`.
- `SupportedOSPlatformVersion`: `10.0.17763.0` (Windows 10 1809+).
- `Nullable` و`ImplicitUsings` مفعّلان، `LangVersion=latest`، `RuntimeIdentifiers=win-x64`.
- `SatelliteResourceLanguages=en` بعد تحويل الواجهة إلى الإنجليزية.

---

## 2. المشاريع والمسؤوليات

### 2.1 `Adawat.Core`

لا يحتوي نوافذ، لكنه يستخدم WPF (`UseWPF=true`) لأن تحويل RTF/HTML يعتمد على `FlowDocument`.

| المجموعة | الملفات | المسؤولية |
|---|---|---|
| النموذج | `Operation.cs`, `FileKind.cs`, `Output.cs` | تعريف العملية وأنواع الملفات ومكان الإخراج |
| الكتالوج | `Catalog.cs` | توليد خيارات العجلة حسب النوع والوضع |
| المستندات | `Documents/Doc.cs`, `DocumentOps.cs`, `Docx.cs`, `Formats.cs`, `HtmlReader.cs`, `Office.cs`, `SheetOps.cs`, `SubtitleOps.cs` | نموذج مستند موحد وقراءة/كتابة الصيغ |
| PDF | `Pdf/PdfOps.cs`, `Platform/PdfPrinter.cs` | قراءة/دمج/تقسيم/تحويل/طباعة PDF |
| الصور | `Images/*` | تحويل، ضغط، معالجة، OCR، QR، بيانات وصفية |
| الوسائط | `Media/*` | FFmpeg وتحويلات الصوت/الفيديو |
| المنصة | `Platform/Clipboard.cs`, `Recognition.cs`, `Rtf.cs`, `QrDrawing.cs`, `FlowHtml.cs` | جسور مع النظام |
| الإعدادات | `Settings.cs` | إعدادات JSON في `%APPDATA%\Adawat\settings.json` |
| الإجراءات | `FileActions.cs` | نسخ/نقل/مشاركة/Float/طرفية، مع Hooks يملؤها التطبيق |
| الترجمة النصية | `L10n.cs` | تمرير إنجليزي (بعد إزالة العربية من الواجهة) |

### 2.2 `Adawat.App`

| المجموعة | الملفات | المسؤولية |
|---|---|---|
| البدء | `Program.cs` | `Main`، `AdawatApp`، ربط المكونات |
| السحب | `DragMonitor.cs` | خطاف ماوس + كشف المفتاح أثناء السحب |
| العجلة | `Wheel/WheelWindow.cs`, `Wheel/QuickWheelWindow.cs` | عجلة الإفلات والعجلة السريعة |
| التنفيذ | `Runner.cs`, `Hud.cs`, `Sounds.cs` | تشغيل العمليات، Toast، الأصوات |
| النوافذ | `UI.cs` (`ToolWindow`), `ToolWindows.cs`, `Windows/Catalog.cs` | إطار مشترك وسجل نوافذ |
| Float | `Floats.cs`, `FloatDialogs.cs` | نافذة عائمة وقائمة سياقها |
| الحافظة | `ClipboardWindow.cs` | سجل نصي/صور، بحث، تثبيت، تحرير، معاينة |
| المفكرة | `NotepadWindow.cs` | ملاحظات، بحث، تثبيت، تحرير، تصدير |
| الإعدادات | `SettingsWindow.cs` | واجهة الإعدادات + بدء التشغيل |
| العرض | `MarkdownViewer.cs` | Markdown داخل `FlowDocumentScrollViewer` |
| الشاشة | `ScreenCapture.cs`, `ScreenTextExtractor.cs`, `TextOverlayWindow.cs`, `ScreenshotPreviewWindow.cs` | التقاط، OCR، طبقة نص، معاينة |
| أدوات مساعدة | `QrScanner.cs`, `QrResultWindow.cs`, `QuickLauncherWindow.cs`, `BluetoothSender.cs`, `KeyboardTypoFixer.cs`, `ShareUI.cs`, `ShellThumbnail.cs` | وظائف سريعة |
| النظام | `Tray.cs`, `GlobalHotkeys.cs`, `Native.cs`, `Theme.cs` | شريط النظام، اختصارات عامة، P/Invoke، ألوان |

### 2.3 `Adawat.Cli`

`adawat-cli` يعيد استخدام `Core` لتشغيل الأدوات من سطر الأوامر، ويضم فحص إصدار WebView2. لا يشارك النوافذ.

---

## 3. مسار التشغيل الأساسي

```
Program.Main
  ├─ --snapshot*  → Snapshots.Run (وضع اختبار/توثيق)
  └─ AdawatApp
       ├─ Mutex (نسخة واحدة)
       ├─ DragMonitor.Start()
       │    ├─ مفتاح أثناء السحب → WheelWindow.Show(mode, point)
       │    └─ نهاية السحب → إخفاء العجلة بعد مهلة
       ├─ FileActions hooks (Float / Share / ChooseFolder)
       ├─ Tray
       ├─ GlobalHotkeys (Float / Quick Wheel / Clipboard listener)
       └─ WelcomeWindow عند أول تشغيل

سحب ملفات فوق العجلة:
  WheelWindow.OnDrag → Catalog.For(mode, files) → Draw()
  WheelWindow.OnDrop → Runner.Start(operation, files)
       ├─ operation.Window موجود → ToolWindows.Open(name, files)
       │      └─ Windows.Catalog.RegisterAll → نافذة الأداة
       └─ غير ذلك → OperationWork مباشرة
              ├─ Hud.Working
              ├─ OperationContext (تقدم/إلغاء)
              ├─ نتيجة → Float أو مكان الإخراج
              └─ Hud.Success/Failure + Sounds
```

### 3.1 أوضاع العجلة

| الوضع | المصدر | المحتوى |
|---|---|---|
| `Convert` | مفتاح التحويل أثناء السحب | صيغ التحويل حسب نوع الملف |
| `Tools` | مفتاح الأدوات | أدوات النوع (ضغط، بيانات وصفية، تحرير، OCR...) |
| `Actions` | مفتاح الإجراءات | Share / Float / Copy / Move / Copy Path / Terminal |

### 3.2 العجلة السريعة

`QuickWheelWindow` مستقلة عن السحب؛ تُفتح باختصار عام وتعرض أدوات مفعّلة من `Settings.EnabledQuickTools`: لقطة شاشة، Bluetooth، QR، إصلاح لوحة المفاتيح، Launcher، Notepad، Clipboard، استخراج نص.

---

## 4. أنماط التواصل

| النمط | الاستخدام |
|---|---|
| سجل مركزي | `ToolWindows.Register/Open` يربط اسم العملية بالنافذة |
| Hooks | `FileActions.ShareHandler/FloatHandler/ChooseFolder` يملؤها `AdawatApp` |
| أحداث | `DragMonitor.KeyDuringDrag/DragEnded`، `ClipboardHistory.Changed` |
| Singleton | `Settings.Current`، `Hud.Shared`، `Sounds` |
| JSON محلي | `settings.json`، `clipboard.json`، `notes.json`، `shortcuts.json`، `clipboard_images/` |
| P/Invoke | `Native.cs` للنوافذ والخطافات والاختصارات |
| COM | `BluetoothSender`، تحويلات Office |

---

## 5. قرارات معمارية مهمة

1. **WPF بلا MVVM:** الواجهات مبنية بالكود داخل ملفات النوافذ. هذا يسرّع التطوير لكنه يصعّب الاختبار وإعادة الاستخدام.
2. **فصل Core/App:** منطق التحويل معزول عن النوافذ، لكن `Core` يعتمد على WPF بسبب `FlowDocument`، فلا يمكن استخدامه في واجهة غير WPF دون تعديل.
3. **الإعدادات Singleton:** `Settings.Current` يُحمَّل مرة واحدة ويُحفظ عند كل تغيير. لا توجد طبقة مراقبة تغييرات.
4. **نافذة `ToolWindow` موحدة:** إطار واحد (رأس/جسم/تذييل) لكل النوافذ، مع `SizeToContent.Height`. هذا سبب مشكلة الإعدادات الطويلة.
5. **الثيم ثابت:** `Theme` يحمل `Brush` جاهزة، وأنماط الأزرار تُخزَّن في `Dictionary` ثابت؛ لا يوجد تبديل وقت التشغيل.
6. **الإغلاق التلقائي:** تعتمد بعض النوافذ على `Deactivated` للإغلاق، وهو أصل D-01.
7. **العجلة مبنية على OLE Drag & Drop:** الملفات تأتي من `DataFormats.FileDrop`، والعملية تُنفَّذ بعد انتهاء السحب عبر `Dispatcher.BeginInvoke`.
8. **لا اختبارات آلية:** التحقق يدوي؛ لذلك كل سلوكيات وقت التشغيل تحتاج المستخدم.

---

## 6. نقاط الضعف المعمارية الحالية

| النقطة | الأثر |
|---|---|
| لا حالة دورة حياة موحدة للنوافذ | D-01/D-02 وأخطاء عند إضافة قوائم سياق أو حوارات |
| الثيم غير قابل للتبديل | F-12/G-03 يتطلبان إعادة بناء الأنماط |
| ~~لا طبقة عرض مستندات~~ | أُضيفت في المرحلة 4: `MarkdownViewer` + `DocumentViewer` + `PdfView` |
| ~~`MarkdownViewer` نصي فقط~~ | أُصلح: GFM/LaTeX/صور/روابط/PDF عبر WPF أصلي |
| لا خدمة شبكة | B-01/B-02 من الصفر |
| لا تجريد ترجمة | A-01/A-03/A-02 من الصفر |
| تكرار Clipboard/Notepad | E-01/R-01/R-02 وصيانة أصعب |
| `SettingsWindow` بلا تمرير | H-01 وأي إعداد جديد |
| مراجع غير مستخدمة | التباس حول الجاهزية وحجم التوزيع |
| لا CI/اختبارات | مخاطر الانحدار عند كل تعديل |

---

## 7. إضافات مقترحة (غير منفذة)

> هذه ليست كوداً قائماً، بل اتجاه معماري مقترح للخارطة.

### 7.1 أساس الثيم

- `ThemeService` يحمل الوضع (`Light/Dark/System`) ويصدر رموز ألوان.
- `ThemeTokens` (خلفية، سطح، نص، نص ثانوي، حدود، تمييز، نجاح، خطأ).
- إعادة بناء `UI` و`ToolWindow` عند تغيّر الثيم بدل التخزين الثابت.

### 7.2 عارض المستندات

> **نُفِّذ أصلياً في المرحلة 4 (لا WebView2):** `MarkdownViewer` معاينة بسيطة للحافظة/المفكرة، و`DocumentViewer` عارض متعدد التبويبات من العجلتين، و`PdfView` صفحات صور عند الطلب بحد ذاكرة 256MB.

- `FlowKit`/`MarkdownFlow`/`DocFlow`/`TexFlow`/`MathText`: Markdig AST و`Doc` وLaTeX ← `FlowDocument` واحد بفرش الثيم.
- `PdfOps.PageSizes`/`RenderPagePng` + `PdfView`: صفحات عند الطلب، LRU 256MB، وحفظ المسار ونسبة التمرير.
- `PreviewTabsStore`: مسارات التبويبات وترتيبها وتمريرها وتكبيرها في `preview-tabs.json`.
- مكوّن واحد يعيد استخدامه: Clipboard Preview، Notes Preview، Wheel File Viewer.
- لا KaTeX ولا Highlight.js ولا Mermaid ولا PDF.js ولا أصول خارجية.

### 7.3 الترجمة

- `ITranslationProvider` (`TranslateAsync(text, source, target, ct)`).
- `OnlineTranslationProvider` أولاً.
- `OnnxTranslationProvider` مستقبلاً خلف الواجهة نفسها.
- `TranslationWindow` بنمط `ToolWindow`، مع حالة معالجة وإلغاء.

### 7.4 الشبكة

- `NetworkStatsService` يقرأ عدادات المحول دورياً.
- `NetworkUsageStore` يحفظ الإجماليات اليومية/الأسبوعية/الشهرية.
- نافذة عرض لحظية + إعداد اختيار المحول.

### 7.5 مكونات واجهة مشتركة

- `ContextMenuBuilder` موحد.
- `EntryCard`/`EntryList` مشترك بين الحافظة والمفكرة.
- `SettingsSection`/`SettingsPage` لتنظيم الإعدادات.
- `WindowLifecycle` أو سلوك داخل `ToolWindow` لمنع الإغلاق المتكرر.

---

## 8. قيود يجب مراعاتها عند التعديل

1. لا تغيير في سلوك الأدوات القائمة دون طلب.
2. لا حذف Arabic OCR/RTL/keyboard fixer/folder matching؛ الإنجليزية تخص الواجهة فقط.
3. لا إضافة مكتبات دون مبرر والتحقق من توافق .NET 10.
4. لا بناء/تشغيل/اختبار من الوكيل.
5. أي تغيير في `Theme` أو `UI` أو `ToolWindow` يؤثر على كل النوافذ، فيجب مراجعته كتغيير عالي المخاطر.
