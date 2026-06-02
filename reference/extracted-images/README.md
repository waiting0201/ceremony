# 從 RDLC 抽取的內嵌圖片

從舊系統 RDLC 報表 XML 解出來的內嵌圖片（base64 → PNG/JPG），供新系統 QuestPDF 還原版面使用。

## 檔案清單

| 檔案 | 大小 | 來源 RDLC | 用途 |
|---|---|---|---|
| `worship2.png` | 63 KB | 6 個 tmpWorship*.rdlc 共用 | **普桌唯一使用的背景圖**（位置 0.26cm, 0.42cm，尺寸 20.05cm × 28.88cm，FitProportional）|
| `worship.png` | 79 KB | tmpWorship*.rdlc 內嵌但未引用 | 死資源（保留備查）|
| `worship1.jpg` | 74 KB | tmpWorship*.rdlc 內嵌但未引用 | 死資源（保留備查）|
| `tablet-tablet.jpg` | 165 KB | tmpTablet.rdlc 內嵌 | 牌位背景或裝飾 — 在原版未直接被 `<Image>` 元件引用，可能為設計者預存 |

## 關鍵發現

1. **6 個 tmpWorship*.rdlc 都用同一張 `worship2`**（不是 worship2/3/4/5 分別對應變體）
2. **每個 worship RDLC 都內嵌 3 張圖**（worship / worship1 / worship2），但實際只用 worship2 — 另兩張是設計者過程留下的 artifact
3. **tmpTablet.rdlc 內嵌 1 張 tablet.jpg**，但 `<Image>` 元件中沒看到引用 — 可能用於設計參考或文化裝飾，新版可不必還原

## 解析方式

用 Python 解析 XML，取出 `<EmbeddedImage>` 內 `<ImageData>` 的 base64 字串解碼：

```python
import xml.etree.ElementTree as ET
import base64

tree = ET.parse('tmpWorship.rdlc')
root = tree.getroot()
for img in root.iter():
    tag = img.tag.split('}')[-1]
    if tag == 'EmbeddedImage':
        name = img.get('Name')
        for child in img:
            t = child.tag.split('}')[-1]
            if t == 'MIMEType':
                mime = child.text
            elif t == 'ImageData':
                data = child.text
        with open(f'{name}.png', 'wb') as o:
            o.write(base64.b64decode(data))
```

## 新系統建議

- **worship2.png** 複製到 `assets/images/worship2.png`，供新版普桌列印背景使用
- **其他 dead 圖**不需移植
- 若客戶提供更高解析度版本應優先使用（內嵌 PNG 應在 DPI 上適合 A4 列印，但可確認）
