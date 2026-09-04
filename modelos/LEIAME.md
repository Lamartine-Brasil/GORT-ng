# Modelos do motor de reconhecimento

Esta pasta guarda os modelos ONNX do **motor de reconhecimento moderno embarcado**
(RF-121), que é o motor do Apêndice A: ONNX Runtime com RapidOCR.

Os arquivos **não entram no controle de versão** — somam mais de 25 MB e são
binários. É `RapidOcrEngine` quem os procura aqui, seguindo RF-128, e o motor se
declara indisponível com explicação quando não os encontra (RF-575), em vez de
falhar no meio de uma tradução.

Quais modelos servem a quais idiomas está declarado em `data/engines.toml`, na
seção `[modern_ocr]`, porque isso é **dado e não código** (RF-029). Acrescentar um
idioma é acrescentar uma entrada lá e pôr o arquivo aqui.

## Arquivos esperados

| Arquivo | Papel | Origem |
|---|---|---|
| `ch_PP-OCRv4_det_infer.onnx` | Detecção de linhas (DBNet), comum a **todos** os idiomas | pacote `rapidocr_onnxruntime` no PyPI |
| `ch_PP-OCRv4_rec_infer.onnx` | Reconhecimento de inglês e latino | idem |
| `japan_PP-OCRv3_rec_infer.onnx` | Reconhecimento de japonês | `cycloneboy/japan_PP-OCRv3_rec_infer` no Hugging Face |
| `japan_dict.txt` | Dicionário de caracteres do modelo japonês | idem |

O detector é um só de propósito: ele acha **onde** há texto, não **o que** está
escrito. Só o reconhecedor é específico do idioma.

## Por que o japonês tem modelo próprio

O modelo chinês cobre kanji, latino, dígitos e pontuação japonesa, mas tem
**1 de 46 hiraganas e 3 de 46 katakanas**. Como kana é a maior parte de uma frase
japonesa, texto japonês sairia ilegível com ele — e RF-309 põe o japonês no escopo
desta versão.

## Como obter

```sh
# Detecção e reconhecimento de inglês
curl -sL -o rapidocr.whl \
  "$(curl -s https://pypi.org/pypi/rapidocr-onnxruntime/json \
     | python3 -c 'import json,sys; print(json.load(sys.stdin)["urls"][0]["url"])')"
python3 -c "
import zipfile, os
z = zipfile.ZipFile('rapidocr.whl')
for n in z.namelist():
    if n.endswith('.onnx'):
        open(os.path.basename(n), 'wb').write(z.read(n))
"
rm rapidocr.whl

# Reconhecimento de japonês
BASE=https://huggingface.co/cycloneboy/japan_PP-OCRv3_rec_infer/resolve/main
curl -sL -o japan_PP-OCRv3_rec_infer.onnx "$BASE/model.onnx"
curl -sL -o japan_dict.txt               "$BASE/japan_dict.txt"
```

O dicionário do modelo de inglês vem embutido nos metadados do próprio arquivo
`.onnx`; o do japonês vem em arquivo separado. O reconhecedor aceita as duas
convenções.
