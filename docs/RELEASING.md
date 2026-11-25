# Публикация релизов форка

Приложение обновляет себя из GitHub-репозитория `johndoecesko/v2rayN`
(константа `Global.AppRepo` в `v2rayN/ServiceLib/Global.cs`; для WebAPI —
`WebReleaseChannel.DefaultRepository`, свойство `WebRepository` в
`v2rayN.WebAPI.csproj`). Чтобы автообновление работало, форк должен
публиковать GitHub Releases с **теми же именами файлов, что и upstream**.

Адрес скачивания: `https://github.com/johndoecesko/v2rayN/releases/download/<тег>/<файл>`.
Тег релиза — версия (например `7.25.3`, допустим префикс `v`); проверка идёт
через `https://api.github.com/repos/johndoecesko/v2rayN/releases`.

## Файлы для десктопного приложения (CoreInfoManager.cs)

- `v2rayN-windows-64.zip`
- `v2rayN-windows-arm64.zip`
- `v2rayN-linux-64.zip`
- `v2rayN-linux-arm64.zip`
- `v2rayN-linux-riscv64.zip`
- `v2rayN-linux-loong64.zip`
- `v2rayN-macos-64.zip`
- `v2rayN-macos-arm64.zip`

Для Windows updater дополнительно подставляет суффиксы к имени:
`-desktop.zip` (Avalonia-сборка, если рядом есть `libHarfBuzzSharp.dll`)
и `-aot.zip` (AOT-сборка). Например, `v2rayN-windows-64-desktop.zip`.
Если такого файла нет в релизе, обновление этой сборки не скачается.

## Файлы для WebAPI (v2rayN.WebAPI/Assets/web-assets.json)

Манифест: `WebAPI-update.json`. Для каждой платформы — полный и
«update»-архив:

| Платформа | Полный архив | Архив обновления |
|---|---|---|
| linux-x64 | `v2rayN-linux-64-WebAPI.zip` | `v2rayN-linux-64-WebAPI-update.zip` |
| linux-arm64 | `v2rayN-linux-arm64-WebAPI.zip` | `v2rayN-linux-arm64-WebAPI-update.zip` |
| win-x64 | `v2rayN-windows-64-WebAPI.zip` | `v2rayN-windows-64-WebAPI-update.zip` |
| win-arm64 | `v2rayN-windows-arm64-WebAPI.zip` | `v2rayN-windows-arm64-WebAPI-update.zip` |
| linux-riscv64 | `v2rayN-linux-riscv64-WebAPI.zip` | `v2rayN-linux-riscv64-WebAPI-update.zip` |
| linux-loongarch64 | `v2rayN-linux-loong64-WebAPI.zip` | `v2rayN-linux-loong64-WebAPI-update.zip` |

Ссылки на файлы в манифесте должны вести на
`https://github.com/johndoecesko/v2rayN/releases/download/<тег>/<файл>`
(иной хост или вложенные пути отвергаются проверкой доверия).

## Важно

Пока в форке нет опубликованных релизов, проверка обновлений ничего не
найдёт (пользователь увидит, что установлена последняя версия или ошибку
получения списка релизов). Ядра (Xray, sing-box, mihomo и др.), geo-файлы и
rule-set по-прежнему скачиваются из их собственных репозиториев и от релизов
форка не зависят.

## Нумерация версий

Проверка обновлений понимает только `X.Y.Z` и сравнивает числа. Форк
продолжает нумерацию upstream и увеличивает последнее число: `8.0.1`,
`8.0.2`… Версия задаётся в `v2rayN/Directory.Build.props` (`<Version>`),
тег релиза совпадает с ней (без `v`).

## GPG-ключ для подписи (один раз)

Workflow подписывает файлы релиза и публикует открытый ключ. Ключ нужен
**без пароля** (подпись идёт в пакетном режиме). На своём компьютере:

```bash
gpg --batch --passphrase '' --quick-gen-key "v2rayN RU fork <ваш@email>" ed25519 sign 2y
gpg --list-secret-keys --keyid-format long          # запомните ID ключа
gpg --armor --export-secret-keys <ID> > private.asc
```

Затем на GitHub: **Settings → Secrets and variables → Actions → New repository
secret**, имя `GPG_PRIVATE_KEY`, значение — всё содержимое `private.asc`
(включая строки `-----BEGIN/END PGP PRIVATE KEY BLOCK-----`). После этого
удалите `private.asc` и храните резервную копию ключа в надёжном месте.

## Выпуск релиза

1. Поднимите `<Version>` в `v2rayN/Directory.Build.props` и слейте в master.
2. **Actions → release all platforms → Run workflow**, в поле `release_tag`
   укажите ту же версию (например `8.0.1`).
3. Дождитесь окончания всех сборок, проверьте файлы в Releases.
