import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import { es } from './resources/es'
import { en } from './resources/en'

export const STORAGE_KEY = 'medflow_lang'
export const DEFAULT_LANGUAGE = 'es'

// Registered languages. Adding one means adding a resource module and an entry here.
export const resources = { es: { translation: es }, en: { translation: en } }
export type Language = keyof typeof resources
export const SUPPORTED_LANGUAGES = Object.keys(resources) as Language[]

export const isSupported = (value: unknown): value is Language =>
  typeof value === 'string' && (SUPPORTED_LANGUAGES as string[]).includes(value)

function readStoredLanguage(): Language {
  try {
    const stored = localStorage.getItem(STORAGE_KEY)
    if (isSupported(stored)) return stored
  } catch {
    // storage blocked or unavailable: fall through to the default
  }
  return DEFAULT_LANGUAGE
}

void i18n.use(initReactI18next).init({
  resources,
  lng: readStoredLanguage(),
  fallbackLng: DEFAULT_LANGUAGE,
  interpolation: { escapeValue: false },
  returnNull: false,
})

const syncDocument = (lng: string) => {
  document.documentElement.lang = lng
  document.title = i18n.t('app.documentTitle')
}
syncDocument(i18n.language)
i18n.on('languageChanged', syncDocument)

/** Switch language, remembering the choice on this device when storage allows it. */
export function setLanguage(lng: Language) {
  try {
    localStorage.setItem(STORAGE_KEY, lng)
  } catch {
    // keep working for this session even if the choice cannot be remembered
  }
  return i18n.changeLanguage(lng)
}

export const currentLanguage = (): Language => (isSupported(i18n.language) ? i18n.language : DEFAULT_LANGUAGE)

export default i18n

/**
 * Translate a validation message stored by a zod schema. Messages are resource keys so they follow
 * language changes; a key may carry JSON parameters after a `?`, for example
 * `validation.maxLength?{"field":"intake.fields.address","max":200}` (a `field` parameter is itself a key).
 */
export function tError(t: (key: never, options?: Record<string, unknown>) => string, message: string): string {
  const q = message.indexOf('?')
  if (q < 0) return t(message as never)
  const key = message.slice(0, q)
  try {
    const params = JSON.parse(message.slice(q + 1)) as Record<string, unknown>
    if (typeof params.field === 'string') params.field = t(params.field as never)
    return t(key as never, params)
  } catch {
    return t(key as never)
  }
}
