import { createHash } from "node:crypto";

export function sha256(value) {
  return `sha256:${createHash("sha256").update(value, "utf8").digest("hex")}`;
}

// Keeps a report's paragraph/item boundaries intact. A single overlong item is
// an explicit payload error rather than an arbitrary character-level split.
export function chunkReport(content, maxCharacters) {
  if (!Number.isInteger(maxCharacters) || maxCharacters < 1) {
    throw new Error("QQ text chunk limit must be a positive integer.");
  }

  const sections = String(content).replace(/\r\n/g, "\n").split(/\n{2,}/).filter(Boolean);
  const chunks = [];
  let current = "";

  const push = () => {
    if (current) chunks.push(current);
    current = "";
  };

  for (const section of sections) {
    if (section.length > maxCharacters) {
      push();
      const items = section.split("\n").filter(Boolean);
      for (const item of items) {
        if (item.length > maxCharacters) {
          throw new Error(`QQ_SECTION_ITEM_TOO_LONG: ${item.slice(0, 48)}`);
        }
        const candidate = current ? `${current}\n${item}` : item;
        if (candidate.length > maxCharacters) {
          push();
          current = item;
        } else {
          current = candidate;
        }
      }
      continue;
    }

    const candidate = current ? `${current}\n\n${section}` : section;
    if (candidate.length > maxCharacters) {
      push();
      current = section;
    } else {
      current = candidate;
    }
  }
  push();
  return chunks.map((text, index) => ({ sequence: index + 1, text, hash: sha256(text) }));
}
