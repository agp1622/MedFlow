import { useTranslation } from 'react-i18next'
import { SUPPORTED_LANGUAGES, currentLanguage, setLanguage } from '@/i18n'

/** Two-way (or n-way) language toggle. Labels come from the resources so new languages need no component change. */
export function LanguageSwitcher({ className = '' }: { className?: string }) {
  const { t, i18n } = useTranslation()
  const active = currentLanguage()
  return (
    <div role="group" aria-label={t('language.label')} lang={i18n.language}
      className={`inline-flex items-center rounded-lg bg-gray-100/80 p-0.5 text-xs font-semibold ${className}`}>
      {SUPPORTED_LANGUAGES.map(lng => (
        <button key={lng} type="button" onClick={() => void setLanguage(lng)}
          aria-pressed={active === lng}
          title={i18n.getFixedT(lng)('language.name')}
          className={`px-2.5 py-1 rounded-md transition-colors ${
            active === lng ? 'bg-white text-primary-600 shadow-sm' : 'text-gray-500 hover:text-gray-700'
          }`}>
          {i18n.getFixedT(lng)('language.short')}
        </button>
      ))}
    </div>
  )
}
