<p align="center">
  <img src="windows/assets/AppIcon-256.png" alt="أدوات" width="128">
</p>

<h1 align="center">أدوات · Adawat</h1>

<p align="center">
  <a href="README.md">English</a> · <b>العربية</b>
</p>

<p align="center">
  <a href="https://www.youtube.com/@motalim-ai?sub_confirmation=1"><img alt="اشترك في قناة متعلم على يوتيوب" src="https://img.shields.io/badge/YouTube-%D8%A7%D8%B4%D8%AA%D8%B1%D9%83%20%D9%81%D9%8A%20%D9%82%D9%86%D8%A7%D8%A9%20%D9%85%D8%AA%D8%B9%D9%84%D9%85-FF0000?style=for-the-badge&logo=youtube&logoColor=white"></a>
</p>

<p align="center">
  <a href="https://github.com/motalim-ai/adawat/releases/latest"><img alt="تحميل" src="https://img.shields.io/github/v/release/motalim-ai/adawat?label=Download&color=5B47E6&style=for-the-badge"></a>
  <img alt="Windows 10 و 11" src="https://img.shields.io/badge/Windows-10%20%C2%B7%2011-0078D4?style=for-the-badge&logo=windows11&logoColor=white">
  <a href="LICENSE"><img alt="الرخصة GPL v3" src="https://img.shields.io/badge/License-GPL%20v3-7CF2C8?style=for-the-badge"></a>
</p>

---

<div dir="rtl">

**أدوات** تطبيق صغير بيعيش جنب الساعة على الويندوز. ابدأ اسحب أي ملف، واضغط مفتاح، فتظهر عجلة اختيارات تحت
الماوس على طول. أفلت الملف على الاختيار اللي عاوزه وخلاص: صيغة جديدة، أو حجم أصغر، أو نسخة معدّلة.
من غير رفع ملفات، ومن غير حساب، ومن غير حد استخدامات. كل حاجة بتحصل على جهازك.

## التثبيت

> **شرح بالفيديو ونصايح وأدوات جديدة:** على [قناة متعلم على يوتيوب](https://www.youtube.com/@motalim-ai?sub_confirmation=1).

1. حمّل ملف التثبيت المناسب لجهازك من آخر إصدار:
   - **[Adawat-Setup-x64.exe](https://github.com/motalim-ai/adawat/releases/latest/download/Adawat-Setup-x64.exe)** لمعظم الأجهزة (Intel أو AMD).
   - **[Adawat-Setup-arm64.exe](https://github.com/motalim-ai/adawat/releases/latest/download/Adawat-Setup-arm64.exe)** لأجهزة ARM (زي Snapdragon و Surface Pro X).

   مش متأكد؟ افتح **الإعدادات ← النظام ← حول (Settings ← System ← About)** وبص على **نوع النظام (System type)**.
2. افتح ملف التثبيت. لو الويندوز قالك *Windows protected your PC*، اضغط **More info** وبعدين **Run anyway**.
   ولو المتصفح قالك إن الملف مش بيتحمّل كتير، اختار **Keep**.
3. اضغط **التالي** و **تثبيت**. مش محتاج باسورد المسؤول، لأن أدوات بيتثبت على حسابك انت بس.
4. أدوات هيشتغل جنب الساعة (لو الأيقونة مستخبية، دوّر تحت السهم **^**)، ودليل البداية بيخليك تجرّب مرة.

**ليه الويندوز بيحذّر؟** لأن ملف التثبيت لسه مش موقّع رقميًا، فـ SmartScreen مش عارفه. والتطبيق مفتوح المصدر،
وبصمة SHA-256 لكل ملف تثبيت مكتوبة في صفحة الإصدار.

محتاج Windows 10 (إصدار 1809) أو Windows 11، نسخة 64 بت. وتقدر تشيله في أي وقت من **الإعدادات ← التطبيقات**.

## بيشتغل إزاي؟

1. **اسحب** ملف أو أكتر من مستكشف الملفات أو سطح المكتب أو أي برنامج.
2. **وانت بتسحب، اضغط مفتاح** فتظهر عجلته. العجلة بتفضل مفتوحة لحد ما تفلت الملف.
3. **أفلت** على الاختيار. النتيجة بتتحفظ جنب الملف الأصلي (أو في التنزيلات أو على سطح المكتب حسب اختيارك).

| المفتاح | العجلة | فيها إيه |
| :---: | --- | --- |
| <kbd>Shift</kbd> | **تحويل** | كل الصيغ اللي الملف ممكن يتحوّل لها |
| <kbd>Ctrl</kbd> | **أدوات** | ضغط وتعديل وقص وترتيب وغيرهم حسب نوع الملف |
| <kbd>Alt</kbd> | **إجراءات** | المشاركة و Float ونسخ ونقل ونسخ المسار وفتح Terminal |

تقدر تغيّر المفاتيح من الإعدادات. الأدوات اللي محتاجة اختيارات (زي الضغط والقص والتعديل) بتفتح نافذة صغيرة
الأول، والباقي بيشتغل أول ما تفلت الملف.

## يقدر يعمل إيه؟

### التحويل

| الملف | يتحوّل إلى |
| --- | --- |
| **الصور** (JPEG و PNG و HEIC و WebP و AVIF و TIFF و GIF و BMP و ICO و RAW…) | PNG · JPEG · WebP · AVIF · TIFF · GIF · BMP · ICO · PDF، مع الحفاظ على HDR وملفات الألوان |
| **الفيديو** (MP4 و MOV و MKV و WebM و AVI…) | MP4 · MOV · MKV · WebM · GIF · MP3 · M4A، ومن غير إعادة ترميز لما يكون ممكن |
| **الصوت** (MP3 و M4A و WAV و FLAC و AIFF و OGG و Opus…) | MP3 · M4A · WAV · FLAC · AIFF · OGG · Opus |
| **PDF** | Word · Excel · PowerPoint · EPUB · Markdown · نص · PNG · JPEG |
| **المستندات** (Word و RTF و HTML و Markdown و EPUB و ODT ونص) | PDF · Word · EPUB · Markdown · RTF · HTML · ODT · نص |
| **الجداول** (Excel و ODS و CSV و TSV) | Excel · ODS · CSV · TSV · PDF · HTML |
| **العروض التقديمية** (PowerPoint و ODP) | PDF · PNG · JPEG |
| **الترجمة** (SRT و VTT) | SRT · VTT · نص |
| **الملفات المضغوطة** (ZIP و TAR و TGZ و GZ و RAR و 7Z…) | ZIP · TAR · TGZ، أو فك الضغط |

ملفات HEIC بتتفتح بس مش بيتحوّل ليها. والعروض التقديمية محتاجة PowerPoint أو LibreOffice متثبتين.

### الأدوات

- **الصور:** ضغط (بإعداد جاهز أو حجم محدد أو نسبة) · تعديل (الإضاءة واللون والتفاصيل والمؤثرات) · تعليق
  (أسهم ونص وأشكال وتظليل وأرقام توضيحية وعدسة تكبير) · إضافة خلفية · قص · إخفاء أجزاء (بيكتشف الوجوه
  والنصوص) · تدوير · نسخ النص منها (OCR وبيدعم العربي) · قراءة رمز QR · تعديل أو حذف البيانات ·
  إنشاء PDF · صورة مجمّعة
- **PDF:** ضغط · دمج · ترتيب الصفحات (تحريك وتدوير وتكرار وحذف) · تقسيم · نسخ النص (OCR) · حذف البيانات
- **الفيديو:** ضغط · قص المدة · قص الصورة · تغيير السرعة · حفظ لقطات · تقسيم لمقاطع · إخفاء أجزاء على مدار
  الوقت · دمج · تدوير · كتم الصوت · توحيد مستوى الصوت · حذف البيانات
- **الصوت:** ضغط · قص · قص السكوت · توحيد الصوت (على مرحلتين، معيار EBU R128) · تصفير أجزاء · أحادي أو
  ستيريو · فيديو بموجة الصوت · حذف البيانات
- **رموز QR:** إنشاء رموز لنص أو رابط أو شبكة Wi-Fi، بستة أشكال للنقط وأربعة للزوايا وتدرج ألوان وشعار في
  المنتصف، مع فحص مباشر إن الرمز لسه بيتقري. التصدير PNG أو SVG.

### الأدوات السريعة

لقطة شاشة، قراءة QR من الشاشة، نسخ النص من الشاشة، سجل الحافظة، مفكرة، مشغّل سريع، إصلاح تخطيط لوحة
المفاتيح، إرسال بالبلوتوث، وغيرهم من العجلة السريعة (<kbd>Ctrl</kbd>+<kbd>Space</kbd>).

### الـ Float

رف صغير عائم للملفات. أفلت عليه ملفاتك عشان تبقى قريبة منك، واسحبها منه لأي برنامج، أو ألصقه بحافة الشاشة.
وممكن النتائج تفتح فيه تلقائيًا.

## اللغة

واجهة البرنامج بالإنجليزية. ودعم المحتوى العربي باقي زي ما هو: المستندات بتحافظ على اتجاهها من اليمين
للشمال، واستخراج النص بيشتغل بالعربي لو حزمة OCR العربية متثبتة في ويندوز.

## البناء من الكود

محتاج Windows 10 إصدار 1809 أو أحدث، و [.NET 10 SDK](https://dotnet.microsoft.com/download)، و
[Inno Setup 6](https://jrsoftware.org/isinfo.php) بس لو عايز تبني ملف التثبيت. المشروع بيتبني على الويندوز بس.
كل الأوامر دي PowerShell وبتتشغّل من جذر المستودع.

</div>

```powershell
windows/tools/fetch-ffmpeg.ps1                                  # نسخة ffmpeg الحرة لـ x64 و ARM64
dotnet build windows/src/Adawat.App/Adawat.App.csproj -c Release
windows/tools/build-installer.ps1                               # بيعمل Adawat-Setup-x64.exe و Adawat-Setup-arm64.exe
```

<div dir="rtl">

السكربت `fetch-ffmpeg.ps1` بينزّل نفس نسخة FFmpeg المثبتة لـ `win-x64` و `win-arm64` في
`windows/build/ffmpeg`. والسكربت `build-installer.ps1` بيجهّز النسختين وينسخ ffmpeg جنب التطبيق وبيبني
`windows/installer/Adawat.iss`؛ ولو Inno Setup متثبت في مكان تاني مرّر `-InnoSetup <path to ISCC.exe>`.
وأداة `adawat-cli` بتشغّل أي اختيار من العجلة من سطر الأوامر للتجربة، زي `adawat-cli list photo.jpg`
و `adawat-cli run image.webp photo.jpg` و `adawat-cli doctor`.

<details>
<summary>هيكل المشروع</summary>

| المسار | فيه إيه |
| --- | --- |
| `windows/src/Adawat.Core` | كل التحويلات والأدوات، بيشاركها التطبيق و `adawat-cli` |
| `windows/src/Adawat.App` | أيقونة الصينية، مراقبة السحب، العجلة، الـ Float ونوافذ الخيارات |
| `windows/src/Adawat.Cli` | واجهة سطر الأوامر `adawat-cli` |
| `windows/src/Adawat.Core/Platform` | مساعدات خاصة بالويندوز: OCR، طباعة PDF، الحافظة، RTF، رسم QR |
| `windows/installer`, `windows/tools` | سكربت Inno Setup، وسكربتات ffmpeg والتثبيت |
| `windows/assets` | أيقونة التطبيق |

</details>

## الخصوصية

أدوات عمره ما بيبعت ملفاتك لأي مكان. مافيهوش تتبّع ولا حساب ولا أي خاصية إنترنت.

## الرخصة

أدوات برنامج حر تحت رخصة [GNU General Public License v3.0](LICENSE): تقدر تستخدمه وتدرسه وتشاركه وتعدّل
فيه. ولو وزّعت نسخة معدّلة، لازم تفضل حرة بنفس الرخصة. ونسخة ffmpeg ومكتبات .NET اللي جوه التطبيق ليهم رخصهم
الخاصة، تفاصيلها في [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

اسم **أدوات** وأيقونته خاصين بالمشروع ده ومش داخلين في الرخصة.

## من صنع متعلم

</div>

<p align="center">
  <a href="https://www.youtube.com/@motalim-ai?sub_confirmation=1"><img alt="اشترك في قناة متعلم على يوتيوب" src="https://img.shields.io/badge/YouTube-%D8%A7%D8%B4%D8%AA%D8%B1%D9%83%20%D9%81%D9%8A%20%D9%82%D9%86%D8%A7%D8%A9%20%D9%85%D8%AA%D8%B9%D9%84%D9%85-FF0000?style=for-the-badge&logo=youtube&logoColor=white"></a>
</p>

<div dir="rtl">

أدوات من صنع **متعلم**. الشروحات والأدوات الجديدة وكواليس الشغل موجودين على
**[قناة متعلم على يوتيوب](https://www.youtube.com/@motalim-ai?sub_confirmation=1)**. اشترك عشان تتابع اللي جاي.
ومن [by-wow.com](https://by-wow.com) كمان.

عندك فكرة أو مشكلة أو ترجمة؟ [افتح Issue](https://github.com/motalim-ai/adawat/issues).

</div>
