# Changelog

تمام تغییرات مهم این پروژه در این فایل ثبت می‌شوند. قالب این فایل بر اساس [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) است و پروژه از [Semantic Versioning](https://semver.org/) پیروی می‌کند.

## [Unreleased]

## [1.5.12] - 2026-07-22

### Fixed

- محدودیت اشتباه ارتفاع ۱۰ پیکسلی از ScrollBar عمودی حذف و قالب‌های عمودی و افقی کاملاً جدا شدند.
- Removed the accidental 10-pixel height constraint from vertical scrollbars and split vertical/horizontal templates.

## [1.5.11] - 2026-07-22

### Fixed

- ارتفاع Thumb از ViewportHeight واقعی لیست محاسبه می‌شود و حداقل ارتفاع ظاهری آن به ۴۰ پیکسل افزایش یافت.
- The thumb height now uses the list's real ViewportHeight and has a 40-pixel visual minimum.

## [1.5.10] - 2026-07-22

### Fixed

- موقعیت دستگیره اسکرول‌بار به Minimum، Maximum، Value و Viewport واقعی متصل شد و دیگر در وسط گیر نمی‌کند.
- Bound the scrollbar thumb to the real minimum, maximum, value, and viewport so it no longer remains stuck in the middle.

## [1.5.9] - 2026-07-22

### Fixed

- دستگیره اسکرول‌بار حداقل طول ثابت و قابل‌گرفتن دارد و در محتوای بلند دیگر به شکل نقطه دیده نمی‌شود.
- Gave the scrollbar thumb a fixed usable minimum length so it no longer collapses into a dot for long content.
- وقتی محتوا کوتاه است و نیازی به اسکرول نیست، اسکرول‌بار کاملاً مخفی می‌شود.
- The scrollbar is hidden completely when the content does not require scrolling.

## [1.5.8] - 2026-07-22

### Fixed

- جهت Track اسکرول‌بار به Orientation کنترل متصل شد تا دستگیره عمودی واقعاً رسم و نمایش داده شود.
- Bound the scrollbar track to the control orientation so the vertical thumb is rendered correctly.

## [1.5.7] - 2026-07-22

### Fixed

- نمایش اسکرول‌بار عمودی در صفحه اصلی اجباری شد و ریل و دستگیره با کنتراست واضح‌تر طراحی شدند.
- Forced the main vertical scrollbar to remain visible and increased track/thumb contrast.

## [1.5.6] - 2026-07-22

### Changed

- حرکت چرخ موس به اسکرول پیکسلی نرم، آرام و انیمیشنی تبدیل شد تا پرش بین آیتم‌ها حذف شود.
- Replaced item-based mouse-wheel jumps with slower, animated pixel-based scrolling.

## [1.5.5] - 2026-07-22

### Fixed

- اسکرول‌بار در حالت عادی و هنگام اسکرول با چرخ موس واضح‌تر شد و دستگیره حداقل ارتفاع مناسب دارد.
- Improved scrollbar visibility during mouse-wheel scrolling and added a practical minimum thumb height.

## [1.5.4] - 2026-07-22

### Changed

- آیکن ورود به تنظیمات با آیکن استاندارد چرخ‌دندهٔ ویندوز جایگزین شد.
- Replaced the settings entry icon with the standard Windows gear icon.

## [1.5.3] - 2026-07-22

### Changed

- اسکرول‌بارهای باریک، گرد و مینیمال بدون دکمه‌های فلش؛ با بازخورد ظریف هنگام Hover و Drag.
- Thin, rounded, minimalist scrollbars without arrow buttons, with subtle hover and drag feedback.

### Planned

- بسته‌بندی و انتشار عمومی در Microsoft Store

## [1.5.2] - 2026-07-22

### Changed

- بازگشت خودکار فیلتر دسته‌بندی و فیلتر پین‌ها به نمای پیش‌فرض هنگام اجرای میانبر اصلی پنل
- حفظ فیلتر اختصاصی فقط برای Hotkeyهای دسته‌بندی

## [1.5.1] - 2026-07-22

### Fixed

- ردیابی مستقل وضعیت Ctrl، Alt و Shift هنگام ضبط Hotkey
- اصلاح ضبط میانبرهای دسته و پنل بدون اجبار به استفاده از کلید Win

## [1.5.0] - 2026-07-22

### Added

- ضبط و ذخیرهٔ Hotkey مستقل برای هر دسته‌بندی
- ثبت هم‌زمان چند میانبر سراسری دسته‌ها
- بازکردن پنل و فیلتر مستقیم روی دستهٔ مرتبط با میانبر
- نمایش میانبر دسته در Tooltip چیپ آن
- تشخیص تداخل یا نامعتبربودن میانبر دسته

## [1.4.0] - 2026-07-22

### Added

- تب مستقل Text Shortcuts در پنجرهٔ تنظیمات
- افزودن Shortcut و Description از داخل تب
- ویرایش مستقیم ردیف‌ها و حذف میانبر انتخاب‌شده
- اعمال یک‌جای تغییرات Text Expansion با ذخیرهٔ تنظیمات

## [1.3.5] - 2026-07-22

### Fixed

- اصلاح binding مقدار انتخاب‌شده و نمایش دوبارهٔ متن داخل ComboBoxهای تنظیمات

## [1.3.4] - 2026-07-22

### Fixed

- سفیدشدن متن مقدار انتخاب‌شدهٔ ComboBoxها در پوستهٔ تیره

### Added

- دکمهٔ آیکنی حذف کل تاریخچه در نوار اصلی با پیام تأیید

## [1.3.3] - 2026-07-22

### Fixed

- اصلاح تب سفید و نامرئی General در پنجرهٔ تنظیمات
- جایگزینی ComboBoxهای سفید پیش‌فرض با قالب هماهنگ با Dark/Light theme
- بهبود حالت انتخاب، Hover، فهرست بازشونده و کنتراست متن تنظیمات
- اصلاح خوانایی کامل صفحهٔ Backup

## [1.3.2] - 2026-07-22

### Added

- بررسی خودکار GitHub Releases هنگام اجرای برنامه با فاصلهٔ حداقل شش ساعت
- انتخاب خودکار فایل Release متناسب با x64 یا ARM64
- دانلود و نصب نسخهٔ جدید با updater مخفی
- راه‌اندازی خودکار برنامه پس از جایگزینی فایل‌ها، بدون نیاز به خروج دستی کاربر
- استفادهٔ مشترک از همین مسیر برای دکمهٔ بررسی آپدیت در صفحهٔ About

## [1.3.1] - 2026-07-22

### Changed

- بازطراحی ظاهری کامل پنجرهٔ تنظیمات با کارت‌ها و رنگ‌بندی هماهنگ با پوسته
- اصلاح خوانایی و محتوای صفحهٔ Backup در پوستهٔ روشن و تیره
- جایگزینی فهرست محدود Hotkey با ضبط زندهٔ ترکیب دلخواه کاربر
- ثبت میانبرهای عمومی سفارشی با Windows RegisterHotKey و حفظ مسیر ویژهٔ Win+V

## [1.3.0] - 2026-07-22

### Added

- System Tray با فرمان‌های بازکردن، تنظیمات و خروج
- کنترل Startup ویندوز از داخل برنامه
- پنجرهٔ تنظیمات مستقل با پنج بخش
- خروجی و بازیابی فایل پشتیبان
- توقف ثبت کلیپ‌بورد، حذف خودکار، استثنای برنامه‌ها و کنترل ذخیرهٔ تصاویر
- پوسته‌های Dark، Light و System
- ظرفیت تاریخچه و اندازهٔ thumbnail قابل تنظیم
- میانبر نمایش `Win+V` یا `Win+C` و پین‌های `Ctrl/Alt + 1…9`
- صفحهٔ About و بررسی نسخهٔ جدید از GitHub
- GitHub Actions برای ساخت و Release خودکار x64/ARM64
- ابزارهای MSIX و امضای اختیاری با secrets

## [1.2.0] - 2026-07-22

### Added

- انتخاب اندازهٔ کوچک، متوسط یا بزرگ برای پنل از منوی تنظیمات
- اعمال آنی اندازه و جای‌گذاری مجدد پنل روی مانیتور فعال
- ذخیرهٔ اندازهٔ انتخاب‌شده برای اجراهای بعدی

## [1.1.1] - 2026-07-22

### Changed

- انتقال تغییر زبان، میانبرهای نوشتاری، پاک‌کردن تاریخچه و خروج کامل به منوی تنظیمات
- خلوت‌ترشدن نوار بالای پنل و حذف دکمهٔ خروج تکراری از پایین پنل

## [1.1.0] - 2026-07-22

### Added

- رابط کاربری کامل فارسی و انگلیسی
- دکمهٔ تغییر آنی زبان داخل پنل اصلی
- تغییر خودکار جهت کلی رابط بین RTL و LTR
- ذخیرهٔ زبان انتخاب‌شده برای اجراهای بعدی
- ترجمهٔ پنل اصلی، پیام‌ها، جزئیات کلیپ‌ها، دسته‌بندی‌ها و میانبرهای نوشتاری

## [1.0.0] - 2026-07-22

### Added

- تاریخچهٔ محلی تا ۲۰۰۰ آیتم متنی، تصویری و فایل
- جایگزینی پنل پیش‌فرض `Win + V`
- پین‌کردن، فیلتر پین‌ها و میانبرهای `Ctrl + 1` تا `Ctrl + 9`
- دسته‌بندی آیکن‌دار برای موارد پین‌شده
- جست‌وجوی زنده و پشتیبانی خودکار RTL/LTR
- thumbnail تصاویر با پردازش و ذخیره‌سازی پس‌زمینه
- حرکت با صفحه‌کلید و چسباندن با Enter یا دابل‌کلیک
- چسباندن در آخرین پنجرهٔ فعال با حفظ وضعیت maximize
- پشتیبانی از چند مانیتور و موقعیت صحیح بر اساس نشانگر موس یا caret
- میانبرهای نوشتاری و پیشنهاد جایگزینی کنار caret
- حالت اجرای مخفی `--startup`
- خروجی‌های self-contained برای Windows x64 و ARM64

[Unreleased]: https://github.com/PouryaRajaei/WinVClipboard/compare/v1.5.12...HEAD
[1.5.12]: https://github.com/PouryaRajaei/WinVClipboard/compare/v1.5.11...v1.5.12
[1.5.11]: https://github.com/PouryaRajaei/WinVClipboard/compare/v1.5.10...v1.5.11
[1.5.10]: https://github.com/PouryaRajaei/WinVClipboard/compare/v1.5.9...v1.5.10
[1.5.9]: https://github.com/PouryaRajaei/WinVClipboard/compare/v1.5.8...v1.5.9
[1.5.8]: https://github.com/PouryaRajaei/WinVClipboard/compare/v1.5.7...v1.5.8
[1.5.7]: https://github.com/PouryaRajaei/WinVClipboard/compare/v1.5.6...v1.5.7
[1.5.6]: https://github.com/PouryaRajaei/WinVClipboard/compare/v1.5.5...v1.5.6
[1.5.5]: https://github.com/PouryaRajaei/WinVClipboard/compare/v1.5.4...v1.5.5
[1.5.4]: https://github.com/PouryaRajaei/WinVClipboard/compare/v1.5.3...v1.5.4
[1.5.3]: https://github.com/PouryaRajaei/WinVClipboard/compare/v1.5.2...v1.5.3
[1.5.2]: https://github.com/PouryaRajaei/WinVClipboard/compare/v1.5.1...v1.5.2
[1.5.1]: https://github.com/PouryaRajaei/WinVClipboard/compare/v1.5.0...v1.5.1
[1.5.0]: https://github.com/PouryaRajaei/WinVClipboard/compare/v1.4.0...v1.5.0
[1.4.0]: https://github.com/PouryaRajaei/WinVClipboard/compare/v1.3.5...v1.4.0
[1.3.5]: https://github.com/PouryaRajaei/WinVClipboard/compare/v1.3.4...v1.3.5
[1.3.4]: https://github.com/PouryaRajaei/WinVClipboard/compare/v1.3.3...v1.3.4
[1.3.3]: https://github.com/PouryaRajaei/WinVClipboard/compare/v1.3.2...v1.3.3
[1.3.2]: https://github.com/PouryaRajaei/WinVClipboard/compare/v1.3.1...v1.3.2
[1.3.1]: https://github.com/PouryaRajaei/WinVClipboard/compare/v1.3.0...v1.3.1
[1.3.0]: https://github.com/PouryaRajaei/WinVClipboard/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/PouryaRajaei/WinVClipboard/compare/v1.1.1...v1.2.0
[1.1.1]: https://github.com/PouryaRajaei/WinVClipboard/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/PouryaRajaei/WinVClipboard/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/PouryaRajaei/WinVClipboard/releases/tag/v1.0.0
