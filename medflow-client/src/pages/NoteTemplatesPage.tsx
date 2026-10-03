import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { ParseKeys } from 'i18next'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useNoteTemplates, useSaveNoteTemplate, useDeleteNoteTemplate } from '@/hooks/queries'
import { PageHeader } from '@/components/layout/AppLayout'
import { PageSpinner, EmptyState, Spinner, Badge } from '@/components/ui'
import { Modal } from './PatientsPage'
import { Trash2, Pencil } from 'lucide-react'
import type { NoteTemplateDto } from '@/types'

const schema = z.object({
  name: z.string().trim().min(1, 'templates.nameRequired').max(100, 'templates.nameMax'),
  body: z.string().refine(v => v.trim().length > 0, 'templates.bodyRequired').refine(v => v.length <= 5000, 'templates.bodyMax'),
})
type TemplateForm = z.infer<typeof schema>

export function NoteTemplatesPage() {
  const { t } = useTranslation()
  const { data, isLoading } = useNoteTemplates()
  const del = useDeleteNoteTemplate()
  const [editing, setEditing] = useState<NoteTemplateDto | 'new' | null>(null)

  return (
    <div className="flex flex-col h-full overflow-hidden">
      <PageHeader title={t('templates.title')} subtitle={t('templates.subtitle')}
        action={{ label: t('templates.new'), onClick: () => setEditing('new') }} />
      {isLoading ? <PageSpinner /> : (
        <div className="flex-1 overflow-auto px-8 py-6 space-y-3">
          {data?.length === 0 && <EmptyState title={t('templates.empty')} />}
          {data?.map(tpl => (
            <div key={`${tpl.isBuiltIn}-${tpl.id}`} className="card p-4 flex items-start justify-between gap-4">
              <div className="min-w-0">
                <p className="font-semibold text-gray-800 text-sm">
                  {tpl.name} {tpl.isBuiltIn && <Badge status="Built-in" />}
                </p>
                <pre className="text-xs text-gray-500 mt-2 whitespace-pre-wrap font-sans">{tpl.body}</pre>
              </div>
              {!tpl.isBuiltIn && (
                <div className="flex gap-1 flex-shrink-0">
                  <button className="btn-ghost p-1.5" aria-label={t('templates.edit')} onClick={() => setEditing(tpl)}>
                    <Pencil size={13} className="text-gray-400" />
                  </button>
                  <button className="btn-ghost p-1.5" aria-label={t('templates.delete')}
                    onClick={() => window.confirm(t('templates.confirmDelete', { name: tpl.name })) && del.mutate(tpl.id)}>
                    <Trash2 size={13} className="text-gray-400" />
                  </button>
                </div>
              )}
            </div>
          ))}
        </div>
      )}
      {editing && (
        <TemplateModal template={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} />
      )}
    </div>
  )
}

function TemplateModal({ template, onClose }: { template?: NoteTemplateDto; onClose: () => void }) {
  const { t } = useTranslation()
  const save = useSaveNoteTemplate()
  const { register, handleSubmit, formState: { errors } } = useForm<TemplateForm>({
    resolver: zodResolver(schema),
    defaultValues: { name: template?.name ?? '', body: template?.body ?? '' },
  })
  const onSubmit = (data: TemplateForm) => save.mutate({ id: template?.id, data }, { onSuccess: onClose })

  return (
    <Modal title={template ? t('templates.edit') : t('templates.new')} onClose={onClose}>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div>
          <label className="label">{t('templates.name')}</label>
          <input className="input" {...register('name')} />
          {errors.name && <p className="text-red-500 text-xs mt-1">{t(errors.name.message as ParseKeys)}</p>}
        </div>
        <div>
          <label className="label">{t('templates.body')}</label>
          <textarea className="input resize-none" rows={10} {...register('body')} />
          {errors.body && <p className="text-red-500 text-xs mt-1">{t(errors.body.message as ParseKeys)}</p>}
        </div>
        <div className="flex justify-end gap-2">
          <button type="button" className="btn-ghost" onClick={onClose}>{t('common.cancel')}</button>
          <button type="submit" className="btn-primary" disabled={save.isPending}>
            {save.isPending ? <Spinner className="w-4 h-4" /> : t('common.save')}
          </button>
        </div>
      </form>
    </Modal>
  )
}
