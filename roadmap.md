# Dari — Roadmap

---

## Часть 1 — `Dari.Archiver` (библиотека) ✅ Реализована

> **Цель:** Высокопроизводительная, полностью управляемая библиотека .NET 10 / C# 13  
> для чтения и записи архивов `.dar` формата Dari v5.

### Структура проекта

```
Dari.Archiver/
├── Format/            # Примитивы формата Dari v5
│   ├── DariConstants.cs         # Магические байты, версия, фиксированные размеры
│   ├── DariHeader.cs            # 13-байтовый заголовок
│   ├── DariFooter.cs            # 15-байтовый футер
│   ├── IndexEntry.cs            # 85-байтовая фиксированная часть + path + extra
│   ├── IndexFlags.cs            # [Flags] LinkedData=0x0001, EncryptedData=0x0002
│   ├── CompressionMethod.cs     # None=0, Brotli=1, Zstandard=2, Lzma=3, LeptonJpeg=4
│   ├── Blake3Hash.cs            # 32-байтовый value type с IEquatable<T>
│   └── FileMetadata.cs          # mtime, uid, gid, perm
│
├── IO/                # Низкоуровневый двоичный I/O
│   ├── DariReader.cs            # Stream → заголовок / футер / индекс / блоки данных
│   ├── DariWriter.cs            # Запись заголовка, блоков, индекса, футера
│   └── BinaryHelpers.cs         # Span-обёртки над BinaryPrimitives
│
├── Compression/       # Пайплайн сжатия
│   ├── ICompressor.cs           # CompressAsync / DecompressAsync
│   ├── CompressorRegistry.cs    # CompressionMethod → ICompressor; ext → метод
│   ├── NoneCompressor.cs
│   ├── BrotliCompressor.cs      # quality=6, lgwin=22
│   ├── ZstandardCompressor.cs   # ZstdSharp, level=3
│   └── LzmaCompressor.cs        # SharpCompress XZ, preset=9
│
├── Crypto/            # Шифрование ChaCha20-Poly1305
│   ├── DariEncryption.cs        # Derivation KDF, Encrypt, Decrypt
│   └── DariPassphrase.cs        # Value-object; ZeroMemory при Dispose
│
├── Deduplication/
│   └── DeduplicationTracker.cs  # checksum → (offset, method); LinkedData entries
│
├── Extra/
│   ├── ExtraField.cs            # Парсинг / сериализация "k=v;k=v"
│   └── WellKnownExtraKeys.cs    # "e", "en", "et", "imk", "imd", "idt" …
│
├── Ignoring/          # Фильтрация по .darignore / .gitignore
│   ├── IIgnoreFilter.cs
│   └── GitIgnoreFilter.cs       # Иерархическая загрузка через пакет Ignore
│
├── Archiving/         # Высокоуровневый API
│   ├── ArchiveReader.cs         # Открыть, перебрать, извлечь
│   ├── ArchiveWriter.cs         # Создать архив, добавить файлы / директории
│   └── ArchiveAppender.cs       # Атомарное дополнение существующего архива
│
└── Diagnostics/
    └── DariFormatException.cs   # Нарушение формата Dari v5
```

### Зависимости

| Пакет | Версия | Назначение |
|-------|--------|------------|
| `Blake3` | 2.2.1 | Контрольные суммы BLAKE3 и KDF |
| `ZstdSharp.Port` | 0.8.7 | Сжатие Zstandard (чистый managed) |
| `SharpCompress` | 0.47.3 | Сжатие LZMA/XZ |
| `Ignore` | 0.2.1 | Парсинг правил `.gitignore` (spec 2.29.2) |

### Реализованные фазы

| Фаза | Описание | Статус |
|------|----------|--------|
| 1 | Примитивы формата — `DariConstants`, `DariHeader`, `DariFooter`, `IndexEntry` | ✅ |
| 2 | Низкоуровневый читатель `DariReader` | ✅ |
| 3 | Низкоуровневый писатель `DariWriter` | ✅ |
| 4 | Сжатие — `ICompressor`, `CompressorRegistry`, Brotli/Zstd/LZMA/None | ✅ |
| 5 | Шифрование — `DariEncryption`, `DariPassphrase` | ✅ |
| 6 | Дополнительные поля — `ExtraField`, `WellKnownExtraKeys` | ✅ |
| 7 | Высокоуровневый API — `ArchiveReader`, `ArchiveWriter` | ✅ |
| 8 | Дедупликация — `DeduplicationTracker`, `LinkedData` entries | ✅ |
| 9 | Дополнение архивов — `ArchiveAppender` (атомарный rename-swap) | ✅ |
| 10 | Тесты — 159 xUnit-тестов (все проходят) | ✅ |

### Поддержка `.darignore` / `.gitignore`

`GitIgnoreFilter` иерархически загружает файлы `.darignore` и `.gitignore` из каждой
директории дерева. `ArchiveWriter.AddDirectoryAsync` автоматически применяет фильтр.
Проверено на реальном архиве — количество файлов совпадает с эталонной реализацией (561 запись).

---

## Часть 2 — `Dari.App` (GUI-приложение)

> **Цель:** Кроссплатформенное GUI-приложение (.NET 10, Avalonia 11) для работы  
> с архивами `.dar` на Windows, macOS и Linux.

### Цели

| Цель | Описание |
|------|----------|
| **Кроссплатформенность** | Windows 10+, macOS 12+, Linux (X11/Wayland) — единая кодовая база |
| **Современный UI** | Avalonia 11 + Fluent-тема; нативный заголовок на каждой платформе |
| **MVVM** | CommunityToolkit.Mvvm; нет code-behind логики |
| **Отзывчивость** | Все I/O операции асинхронны; прогресс и отмена через `CancellationToken` |
| **Безопасность** | Пароль хранится только в `DariPassphrase` (ZeroMemory при закрытии) |

### Структура проекта

```
Dari.App/
├── Assets/                  # Иконки, шрифты, ресурсы
├── Controls/                # Переиспользуемые Avalonia-контролы
│   ├── FileIconControl.axaml       # Иконка по расширению файла
│   └── SizeLabel.axaml             # Форматированный вывод размера (KB/MB/GB)
├── Converters/              # IValueConverter для привязок
├── Models/                  # Доменные модели UI-слоя
│   └── ArchiveEntryViewModel.cs
├── Services/                # Абстракции сервисов (для тестов)
│   ├── IDialogService.cs          # Открытие диалогов файлов, уведомлений
│   ├── IClipboardService.cs
│   └── IProgressService.cs
├── ViewModels/
│   ├── MainWindowViewModel.cs     # Главный shell: меню, вкладки, статус
│   ├── ArchiveBrowserViewModel.cs # Просмотр архива, фильтр, сортировка
│   ├── CreateArchiveViewModel.cs  # Мастер создания нового архива
│   ├── ExtractViewModel.cs        # Прогресс извлечения
│   ├── PasswordPromptViewModel.cs # Ввод пароля для зашифрованного архива
│   └── PreviewViewModel.cs        # Предпросмотр содержимого файла
└── Views/
    ├── MainWindow.axaml
    ├── ArchiveBrowserView.axaml
    ├── CreateArchiveView.axaml
    ├── ExtractView.axaml
    ├── PasswordPromptView.axaml
    └── PreviewView.axaml
```

### Зависимости

| Пакет | Назначение |
|-------|------------|
| `Avalonia` 11.x | UI-фреймворк |
| `Avalonia.Themes.Fluent` | Тема оформления |
| `Avalonia.Desktop` | Поддержка native file dialogs |
| `CommunityToolkit.Mvvm` | `[ObservableProperty]`, `[RelayCommand]`, `WeakReferenceMessenger` |
| `Dari.Archiver` | Ссылка на проект библиотеки архиватора |

---

### Фаза A — Инициализация проекта

**Цель:** Создать скелет решения, убедиться что приложение запускается на всех платформах.

- Добавить `Dari.App` как Avalonia Application project в `Dari.slnx`
- Настроить `net10.0` TFM, `Nullable enable`, `LangVersion preview`
- Подключить `Avalonia.Themes.Fluent`, `CommunityToolkit.Mvvm`, `Dari.Archiver`
- Реализовать `MainWindow` с базовой Fluent-темой и пустым контентом
- Настроить `app.manifest` (Windows), `Info.plist` (macOS), `.desktop` (Linux)
- Добавить иконку приложения в форматах `.ico` / `.icns` / `.png`
- Проверить сборку и запуск на Windows, macOS, Linux

---

### Фаза B — Просмотр архива

**Цель:** Открыть `.dar`-файл и отобразить список записей.

**Основные компоненты:**

- `MainWindowViewModel` — команды `OpenArchiveCommand`, `CloseArchiveCommand`; открывает системный диалог выбора файла через `IDialogService`
- `ArchiveBrowserViewModel` — хранит `IReadOnlyList<ArchiveEntryViewModel>`; поддерживает режим плоского списка и дерева директорий; сортировка по имени / размеру / дате / степени сжатия
- `ArchiveBrowserView` — `DataGrid` со столбцами: имя, путь, размер, сжатый размер, % сжатия, алгоритм, дата, права доступа
- `ArchiveEntryViewModel` — оборачивает `IndexEntry`; вычисляемые свойства `CompressionRatio`, `IsEncrypted`, `IsLinked`, иконка по расширению
- Метаданные архива в заголовке: дата создания, количество файлов, общий / сжатый размер
- Строка поиска с real-time фильтрацией (привязка к `SearchText`, `CollectionView` или `ObservableCollection<ArchiveEntryViewModel>`)
- Открытие через Drag & Drop `.dar`-файла на окно
- При зашифрованном архиве — показать `PasswordPromptView` перед открытием

---

### Фаза C — Извлечение

**Цель:** Извлечь выбранные записи или весь архив на диск.

**Основные компоненты:**

- `ExtractViewModel` — список выбранных записей или «все»; путь назначения; `Progress<double>`; `CancellationTokenSource`
- Команды в `ArchiveBrowserViewModel`:
  - `ExtractSelectedCommand` — извлечь отмеченные записи
  - `ExtractAllCommand` — извлечь весь архив
  - `OpenInExplorerCommand` — открыть директорию назначения после извлечения
- `ExtractView` — модальный диалог с прогресс-баром, счётчиком файлов, кнопкой «Отмена»
- Конфликты имён: диалог с выбором «Перезаписать / Пропустить / Переименовать»
- Ошибки контрольной суммы — отдельное уведомление; опция «продолжить несмотря на ошибки»
- По завершении — итоговое уведомление с количеством извлечённых файлов

---

### Фаза D — Создание архива

**Цель:** Создать новый `.dar`-архив из выбранных файлов или директории.

**Основные компоненты:**

- `CreateArchiveViewModel` — мастер из 3 шагов:
  1. **Источник** — выбор директории или отдельных файлов; предпросмотр дерева файлов с учётом `.darignore` / `.gitignore`
  2. **Параметры** — алгоритм сжатия (Brotli / Zstd / LZMA / Авто / Без сжатия); включить дедупликацию (чекбокс); шифрование (пароль + подтверждение)
  3. **Назначение** — путь к результирующему `.dar`; кнопка «Создать»
- Прогресс создания через `ArchiveWriter.AddDirectoryAsync` + `IProgress<(int done, int total, string currentFile)>`
- После создания — открыть созданный архив в браузере
- Создание через Drag & Drop папки на пустое окно приложения

---

### Фаза E — Дополнение архива

**Цель:** Добавить файлы в существующий открытый архив.

**Основные компоненты:**

- Команда `AppendFilesCommand` в `MainWindowViewModel`
- Drag & Drop файлов / папок на открытый `ArchiveBrowserView`
- Диалог выбора файлов для добавления
- Использует `ArchiveAppender.OpenAsync` под капотом
- Показ прогресса и обновление `ArchiveBrowserViewModel` после успешного завершения
- Если архив зашифрован — запросить пароль перед дополнением
- Уведомление об успехе с количеством добавленных файлов / дедуплицированных блоков

---

### Фаза F — Предпросмотр

**Цель:** Показать содержимое выбранного файла без полного извлечения.

**Основные компоненты:**

- `PreviewViewModel` — читает raw-блок через `ArchiveReader.OpenRawBlockAsync`, декодирует на лету; ограничение на размер — не более 1 МБ
- Типы предпросмотра:
  - **Текст** — UTF-8 / Latin-1 / hex-дамп для бинарных файлов; использует `AvaloniaEdit` или встроенный `TextBlock`
  - **Изображения** — `Bitmap` через Avalonia (`png`, `jpg`, `bmp`, `gif`, `webp`)
  - **Прочее** — hex-дамп первых 512 байт
- Панель предпросмотра справа от списка; переключается выбором записи (debounce 150 мс)
- Кнопка «Извлечь и открыть» — извлечение во временную папку, открытие системным приложением

---

### Фаза G — Платформенная интеграция и полировка

**Цель:** Нативное ощущение на каждой платформе; финальная доработка UX.

**Windows:**
- Регистрация ассоциации `.dar` через инсталлятор (NSIS или WiX Toolset)
- Контекстное меню проводника: «Открыть в Dari», «Извлечь здесь»
- Нативный заголовок через `Avalonia.Win32.TitleBarCustomization`

**macOS:**
- `NSDocument`-архитектура через Avalonia; поддержка `Open with…`
- Иконки в форматах `icns` для Finder
- Нативная строка меню (File / Edit / Window / Help)
- Подписание и нотаризация (через `codesign` + `notarytool`)

**Linux:**
- `.desktop`-файл с MIME-type `application/x-dari-archive`
- XDG `MimeInfo.cache` обновляется при установке через `update-mime-database`
- Поддержка Wayland и X11 через Avalonia

**Общее:**
- Тема: светлая / тёмная (следует за системной настройкой `ActualThemeVariant`)
- Горячие клавиши: `Ctrl+O` (открыть), `Ctrl+E` (извлечь), `Ctrl+N` (создать), `Ctrl+W` (закрыть)
- Список недавних файлов в меню (хранится в `%APPDATA%` / `~/.config/dari/recent.json`)
- Настройки: директория извлечения по умолчанию, тема, язык

---

### Тесты приложения

| Тест-класс | Покрытие |
|------------|----------|
| `ArchiveBrowserViewModelTests` | Открытие архива, фильтрация, сортировка |
| `ExtractViewModelTests` | Прогресс, отмена, конфликты имён |
| `CreateArchiveViewModelTests` | Параметры создания, валидация пути |
| `PasswordPromptViewModelTests` | Корректный / неверный пароль |
| `AppendViewModelTests` | Дополнение файлами, дедупликация |
| `IntegrationTests` | Создать → открыть → извлечь (headless Avalonia) |

---

### Порядок реализации

```
Фаза A  →  Инициализация проекта (скелет, тема, запуск на всех платформах)
Фаза B  →  Просмотр архива (открытие файла, DataGrid, поиск)
Фаза C  →  Извлечение (выбранных записей и всего архива)
Фаза D  →  Создание архива (мастер, прогресс, ignore-фильтр)
Фаза E  →  Дополнение архива (Append, Drag & Drop)
Фаза F  →  Предпросмотр (текст, изображения, hex-дамп)
Фаза G  →  Платформенная интеграция (ассоциации, меню, нотаризация)
```

Каждая фаза независимо компилируется и тестируется перед переходом к следующей.
