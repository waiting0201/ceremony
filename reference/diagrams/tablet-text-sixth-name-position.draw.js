const fs = require('fs');
const S = 26;            // px per cm
const PAD = 46;          // inner padding around each paper
const HEAD = 132;        // header band
const GAP = 80;          // gap between the two papers

// ---- cell data: [label, Top(cm), Left(cm), isMain, isProposed] ----
// width/height representative per group
function cell(label, top, left, opt={}) {
  return { label, top, left, main: !!opt.main, prop: !!opt.prop };
}

// TABLET 11.5 x 25.4
const tabletW = 11.5, tabletH = 25.4;
const tDeadW = 0.6, tDeadRep = 1.86;
const tLiveW = 0.7, tLiveRep = 1.44;
const tDead = [
  cell('1', 7.5825, 4.9, {main:true}),
  cell('2', 7.5825, 5.8),
  cell('3', 7.5825, 4.0),
  cell('4', 9.4464, 5.8),
  cell('5', 9.4464, 4.0),
  cell('6', 9.4464, 4.9, {prop:true}),
];
const tLive = [
  cell('1', 14.00389, 1.56167, {main:true}),
  cell('2', 14.00389, 0.83528),
  cell('3', 14.0, 0.1),
  cell('4', 15.44174, 0.83528),
  cell('5', 15.44174, 0.13528),
  cell('6', 15.44174, 1.56167, {prop:true}),
];

// TEXT 36.5 x 26.2
const textW = 36.5, textH = 26.2;
const xDeadW = 0.91251, xDeadRep = 2.06;
const xLiveW = 0.91251, xLiveRep = 1.98;
const xDead = [
  cell('1', 3.65889, 12.41251, {main:true}),
  cell('2', 3.65889, 13.32502),
  cell('3', 3.65889, 11.5),
  cell('4', 5.72264, 13.32502),
  cell('5', 5.72264, 11.5),
  cell('6', 5.72264, 12.41251, {prop:true}),
];
const xLive = [
  cell('1', 15.2748, 21.87382, {main:true}),
  cell('2', 15.2748, 20.96131),
  cell('3', 15.2748, 20.0488),
  cell('4', 17.25916, 20.96131),
  cell('5', 17.25916, 20.0488),
  cell('6', 17.25916, 21.87382, {prop:true}),
];

function paperBlock(ox, oy, title, wCm, hCm, groups) {
  let s = '';
  // paper
  s += `<rect x="${ox}" y="${oy}" width="${wCm*S}" height="${hCm*S}" fill="#fcfcf8" stroke="#888" stroke-width="1.5"/>`;
  s += `<text x="${ox}" y="${oy-14}" font-size="19" font-weight="bold" fill="#222">${title}　<tspan font-size="12" font-weight="normal" fill="#666">紙張 ${wCm}cm × ${hCm}cm（直書，由上往下）</tspan></text>`;
  // faint cm grid every 5cm
  for (let c=0;c<=wCm;c+=5){ s+=`<line x1="${ox+c*S}" y1="${oy}" x2="${ox+c*S}" y2="${oy+hCm*S}" stroke="#eee" stroke-width="1"/>`; }
  for (let r=0;r<=hCm;r+=5){ s+=`<line x1="${ox}" y1="${oy+r*S}" x2="${ox+wCm*S}" y2="${oy+r*S}" stroke="#eee" stroke-width="1"/>`;
    s+=`<text x="${ox-6}" y="${oy+r*S+4}" font-size="9" fill="#bbb" text-anchor="end">${r}</text>`; }

  for (const g of groups) {
    const {cells, cw, rep, name} = g;
    // group bounding label
    let minL=99,maxR=-99,minT=99,maxB=-99;
    for (const c of cells){ minL=Math.min(minL,c.left); maxR=Math.max(maxR,c.left+cw); minT=Math.min(minT,c.top); maxB=Math.max(maxB,c.top+rep);}
    s += `<text x="${ox+minL*S}" y="${oy+minT*S-6}" font-size="13" font-weight="bold" fill="#444">${name}</text>`;
    for (const c of cells) {
      const x = ox + c.left*S, y = oy + c.top*S, w = cw*S, h = rep*S;
      if (c.prop) {
        s += `<rect x="${x}" y="${y}" width="${w}" height="${h}" fill="#fde2e2" stroke="#d62828" stroke-width="2" stroke-dasharray="5 3" rx="2"/>`;
        s += `<text x="${x+w/2}" y="${y+h/2+5}" font-size="14" font-weight="bold" fill="#d62828" text-anchor="middle">${c.label}</text>`;
      } else {
        const fill = c.main ? '#bcd4f0' : '#dcebfb';
        s += `<rect x="${x}" y="${y}" width="${w}" height="${h}" fill="${fill}" stroke="#2563aa" stroke-width="${c.main?2:1.2}" rx="2"/>`;
        s += `<text x="${x+w/2}" y="${y+h/2+5}" font-size="13" fill="#1a3a5c" text-anchor="middle">${c.label}${c.main?'主':''}</text>`;
      }
    }
  }
  return s;
}

const tOx = PAD, tOy = HEAD;
const xOx = PAD + tabletW*S + GAP, xOy = HEAD;
const totalW = xOx + textW*S + PAD;
const totalH = HEAD + Math.max(tabletH, textH)*S + PAD + 40;

const CANVAS = Math.max(totalW, totalH);   // square canvas so qlmanage's square-fit doesn't clip
let svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${CANVAS}" height="${CANVAS}" viewBox="0 0 ${CANVAS} ${CANVAS}" font-family="Helvetica, Arial, sans-serif">`;
svg += `<rect width="${CANVAS}" height="${CANVAS}" fill="#ffffff"/>`;
svg += `<text x="${PAD}" y="34" font-size="22" font-weight="bold" fill="#111">第 6 位往生／陽上 — 建議補的格位（紅色虛線）</text>`;
// legend
const lx = PAD, ly = 56;
svg += `<rect x="${lx}" y="${ly}" width="16" height="12" fill="#bcd4f0" stroke="#2563aa"/><text x="${lx+22}" y="${ly+11}" font-size="12" fill="#333">主名（第1位）</text>`;
svg += `<rect x="${lx+120}" y="${ly}" width="16" height="12" fill="#dcebfb" stroke="#2563aa"/><text x="${lx+142}" y="${ly+11}" font-size="12" fill="#333">現有第2–5位</text>`;
svg += `<rect x="${lx+250}" y="${ly}" width="16" height="12" fill="#fde2e2" stroke="#d62828" stroke-dasharray="4 2"/><text x="${lx+272}" y="${ly+11}" font-size="12" fill="#333">建議補的第6位</text>`;
svg += `<text x="${lx+430}" y="${ly+11}" font-size="11" fill="#777">框=直書姓名格；高度為示意（代表一個約3字的名字）</text>`;

svg += paperBlock(tOx, tOy, '薦牌 tmpTablet（基本變體）', tabletW, tabletH, [
  {cells:tDead, cw:tDeadW, rep:tDeadRep, name:'往生'},
  {cells:tLive, cw:tLiveW, rep:tLiveRep, name:'陽上'},
]);
svg += paperBlock(xOx, xOy, '文牒 tmpText（基本變體）', textW, textH, [
  {cells:xDead, cw:xDeadW, rep:xDeadRep, name:'往生'},
  {cells:xLive, cw:xLiveW, rep:xLiveRep, name:'陽上'},
]);

svg += `</svg>`;
fs.writeFileSync('layout.svg', svg);
console.log('wrote layout.svg', totalW+'x'+totalH);
