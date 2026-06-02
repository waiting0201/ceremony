; 自訂 NSIS include：把預設安裝資料夾固定為英文 Ceremony，
; 同時保留 electron-builder.yml 的中文 productName（app 名 / 捷徑名 / 開始功能表仍是中文）。
; electron-builder 預設安裝路徑 = $PROGRAMFILES64\${productName}（中文）；
; preInit macro 在精靈讀 InstallLocation 前覆寫成 $PROGRAMFILES64\Ceremony。
; 使用者仍可在安裝精靈手動改路徑（allowToChangeInstallationDirectory: true）。
!macro preInit
  SetRegView 64
  WriteRegExpandStr HKLM "${INSTALL_REGISTRY_KEY}" InstallLocation "$PROGRAMFILES64\Ceremony"
  WriteRegExpandStr HKCU "${INSTALL_REGISTRY_KEY}" InstallLocation "$PROGRAMFILES64\Ceremony"
  SetRegView 32
  WriteRegExpandStr HKLM "${INSTALL_REGISTRY_KEY}" InstallLocation "$PROGRAMFILES64\Ceremony"
  WriteRegExpandStr HKCU "${INSTALL_REGISTRY_KEY}" InstallLocation "$PROGRAMFILES64\Ceremony"
!macroend
