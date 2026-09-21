// Régions françaises — 13 régions métropolitaines + 5 DROM (loi NOTRe 2016)
export const REGIONS = [
  // Métropole
  'Auvergne-Rhône-Alpes',
  'Bourgogne-Franche-Comté',
  'Bretagne',
  'Centre-Val de Loire',
  'Corse',
  'Grand Est',
  'Hauts-de-France',
  'Île-de-France',
  'Normandie',
  'Nouvelle-Aquitaine',
  'Occitanie',
  'Pays de la Loire',
  "Provence-Alpes-Côte d'Azur",
  // Départements et régions d'outre-mer
  'Guadeloupe',
  'Guyane',
  'La Réunion',
  'Martinique',
  'Mayotte',
] as const

export type Region = (typeof REGIONS)[number]
