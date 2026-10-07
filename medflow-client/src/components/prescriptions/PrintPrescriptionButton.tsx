import { useTranslation } from 'react-i18next'
import { Printer } from 'lucide-react'
import { usePrintPrescription } from '@/hooks/queries'
import { Spinner } from '@/components/ui'

export function PrintPrescriptionButton({ id }: { id: number }) {
  const { t } = useTranslation()
  const print = usePrintPrescription()
  return (
    <button type="button" className="btn-ghost p-1.5" title={t('prescriptions.print')} aria-label={t('prescriptions.print')}
      disabled={print.isPending} onClick={() => print.mutate(id)}>
      {print.isPending ? <Spinner className="w-4 h-4" /> : <Printer size={14} className="text-gray-400" />}
    </button>
  )
}
