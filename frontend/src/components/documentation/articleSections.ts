// Identifiant d'ancre d'un titre : minuscules, sans accents, mots reliés par des tirets.
// Même calcul pour le sommaire (depuis le Markdown) et pour le titre rendu (depuis son texte).
export function headingId(text: string): string {
  return text
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
}

// Titres de niveau 2 du Markdown, pour le sommaire de l'article. Le balisage de mise en
// forme (**gras**, `code`) est retiré pour obtenir le texte affiché.
export function extractSections(markdown: string): { id: string; title: string }[] {
  return markdown
    .split('\n')
    .filter((line) => line.startsWith('## '))
    .map((line) => line.slice(3).replace(/[*_`]/g, '').trim())
    .map((title) => ({ id: headingId(title), title }))
}
