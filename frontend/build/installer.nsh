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

; 開始功能表加「解除安裝」捷徑。
; electron-builder 預設只建 app 捷徑（createStartMenuShortcut），不含解除安裝；
; 這裡在同一個 $SMPROGRAMS 位置補一個指向其產出的 uninstaller（$INSTDIR\${UNINSTALL_FILENAME}）。
; 升級時舊版 uninstaller 會先靜默移除此捷徑，customInstall 再重建 → 不殘留。
!macro customInstall
  CreateShortCut "$SMPROGRAMS\解除安裝 ${PRODUCT_NAME}.lnk" "$INSTDIR\${UNINSTALL_FILENAME}"
!macroend

; 真正解除安裝時一併移除上面建立的捷徑。
!macro customUnInstall
  Delete "$SMPROGRAMS\解除安裝 ${PRODUCT_NAME}.lnk"
!macroend
