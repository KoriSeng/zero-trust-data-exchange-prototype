#!/usr/bin/env bash
set -euo pipefail

# Generates local sample assets for dataset explorer seeding.
# Outputs files under: infrastructure/modules/s3_poc/sample/

script_dir="$(cd "$(dirname "$0")" && pwd)"
repo_root="$(cd "$script_dir/../.." && pwd)"
sample_root="$repo_root/infrastructure/modules/s3_poc/sample"
tmp_dir="/tmp/ztdx-sample-assets"

echo "Generating sample assets in: $sample_root"
mkdir -p "$sample_root"
rm -rf "$tmp_dir"
mkdir -p "$tmp_dir"

cat > "$tmp_dir/generate.js" <<'JS'
const fs = require('fs');
const path = require('path');
const parquet = require('parquetjs-lite');

const root = process.argv[2];
if (!root) {
  throw new Error('Missing output root');
}

const ensure = (p) => fs.mkdirSync(path.dirname(p), { recursive: true });
const writeText = (rel, content) => {
  const full = path.join(root, rel);
  ensure(full);
  fs.writeFileSync(full, content, 'utf8');
};

// Simple 300x200 gray PNG placeholder.
const pngBase64 =
  'iVBORw0KGgoAAAANSUhEUgAAASwAAADICAIAAADdvUsCAAABiklEQVR4nO3TIQEAQAgAsM3/0h6GQ0p4k6C6dQYAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAPyN1wAB9tmQJQAAAABJRU5ErkJggg==';
const writePng = (rel) => {
  const full = path.join(root, rel);
  ensure(full);
  fs.writeFileSync(full, Buffer.from(pngBase64, 'base64'));
};

async function writeParquet() {
  const schema = new parquet.ParquetSchema({
    sample_id: { type: 'UTF8' },
    gene_symbol: { type: 'UTF8' },
    variant: { type: 'UTF8' },
    cohort: { type: 'UTF8' },
    confidence: { type: 'DOUBLE' }
  });

  const p1 = path.join(root, 'datasets/genomic-2024/variants-part-001.parquet');
  ensure(p1);
  let writer = await parquet.ParquetWriter.openFile(schema, p1);
  for (let i = 1; i <= 16; i++) {
    await writer.appendRow({
      sample_id: `G-A-${String(i).padStart(4, '0')}`,
      gene_symbol: ['BRCA1', 'BRCA2', 'TP53', 'EGFR'][i % 4],
      variant: `c.${110 + i}A>G`,
      cohort: i % 2 ? 'train' : 'validation',
      confidence: 0.81 + (i % 6) * 0.02
    });
  }
  await writer.close();

  const p2 = path.join(root, 'datasets/genomic-2024/variants-part-002.parquet');
  ensure(p2);
  writer = await parquet.ParquetWriter.openFile(schema, p2);
  for (let i = 17; i <= 32; i++) {
    await writer.appendRow({
      sample_id: `G-A-${String(i).padStart(4, '0')}`,
      gene_symbol: ['KRAS', 'NRAS', 'ALK', 'PIK3CA'][i % 4],
      variant: `c.${180 + i}T>C`,
      cohort: i % 3 ? 'test' : 'validation',
      confidence: 0.77 + (i % 8) * 0.018
    });
  }
  await writer.close();
}

async function main() {
  await writeParquet();

  writeText(
    'datasets/genomic-2024/sample-manifest.csv',
    'sample_id,partition,site,consent_version\n' +
      'G-A-0001,train,site-01,v2\n' +
      'G-A-0002,validation,site-03,v2\n' +
      'G-A-0003,test,site-02,v1\n'
  );

  writeText(
    'datasets/proteomics-2024/abundance-matrix.tsv',
    'protein_id\tsubject_id\tabundance\n' +
      'P001\tSUBJ-001\t12.3\n' +
      'P002\tSUBJ-001\t5.1\n' +
      'P001\tSUBJ-002\t9.8\n' +
      'P003\tSUBJ-004\t21.5\n'
  );

  writeText(
    'datasets/proteomics-2024/feature-dictionary.json',
    JSON.stringify(
      {
        abundance: 'Log2 normalized abundance',
        protein_id: 'Synthetic UniProt-like protein identifier',
        subject_id: 'Anonymized participant identifier'
      },
      null,
      2
    )
  );

  writeText(
    'datasets/imaging-ct-2025/ct-series-index.csv',
    'series_id,subject_id,slice_count,spacing_mm\n' +
      'CT-001,SUBJ-101,320,0.75\n' +
      'CT-002,SUBJ-102,280,1.00\n' +
      'CT-003,SUBJ-103,410,0.60\n'
  );

  writeText(
    'datasets/imaging-ct-2025/segmentation-labels.jsonl',
    '{"series_id":"CT-001","label":"nodule","count":3}\n' +
      '{"series_id":"CT-002","label":"consolidation","count":1}\n' +
      '{"series_id":"CT-003","label":"nodule","count":2}\n'
  );

  writePng('thumbnails/genomic-2024.png');
  writePng('thumbnails/proteomics-2024.png');
  writePng('thumbnails/imaging-ct-2025.png');
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
JS

cd "$tmp_dir"
npm init -y --silent >/dev/null
npm install --silent parquetjs-lite
node "$tmp_dir/generate.js" "$sample_root"

echo "Sample assets generated."
