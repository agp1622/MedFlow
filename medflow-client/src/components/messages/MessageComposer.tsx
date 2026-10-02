import { useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Paperclip, Send, X } from 'lucide-react'
import { Spinner } from '@/components/ui'

const MAX_BODY = 4000
const MAX_FILES = 5
const MAX_FILE_BYTES = 50 * 1024 * 1024
// Mirrors the allow-list enforced by the server for all attachments
const ACCEPT = 'image/*,video/mp4,video/webm,video/quicktime,video/x-msvideo,.pdf,.doc,.docx,.xls,.xlsx'

const schema = z.object({
  body: z.string().max(MAX_BODY, `Message is too long (max ${MAX_BODY} characters).`),
})
type Form = z.infer<typeof schema>

interface Props {
  isSending: boolean
  onSend: (body: string, files: File[]) => Promise<unknown>
  showUrgentNotice?: boolean
}

export function MessageComposer({ isSending, onSend, showUrgentNotice }: Props) {
  const { register, handleSubmit, reset, watch, setError, clearErrors, formState: { errors } } =
    useForm<Form>({ resolver: zodResolver(schema), defaultValues: { body: '' } })
  const [files, setFiles] = useState<File[]>([])
  const [fileError, setFileError] = useState<string | null>(null)
  const picker = useRef<HTMLInputElement>(null)
  const length = watch('body').length

  const addFiles = (picked: FileList | null) => {
    if (!picked) return
    const next = [...files, ...Array.from(picked)]
    if (next.length > MAX_FILES) { setFileError(`A message can include at most ${MAX_FILES} files.`); return }
    if (next.some(f => f.size > MAX_FILE_BYTES)) { setFileError('Files must be 50 MB or smaller.'); return }
    setFileError(null)
    setFiles(next)
    if (picker.current) picker.current.value = ''
  }

  const submit = handleSubmit(async ({ body }) => {
    if (!body.trim() && files.length === 0) {
      setError('body', { message: 'Write a message or attach a file.' })
      return
    }
    try {
      await onSend(body.trim(), files)
      reset({ body: '' })
      setFiles([])
      setFileError(null)
    } catch {
      // the mutation shows the server's reason; keep the draft so nothing is lost
    }
  })

  return (
    <form onSubmit={submit} className="border-t border-border pt-3 space-y-2" noValidate>
      {showUrgentNotice && (
        <p className="text-xs text-gray-400">
          For non-urgent questions only. In an emergency, call your doctor's office or emergency services.
        </p>
      )}
      <textarea rows={3} className="input resize-none" placeholder="Write a message…" aria-label="Message"
        {...register('body', { onChange: () => clearErrors('body') })} />
      {files.length > 0 && (
        <ul className="flex flex-wrap gap-2">
          {files.map((f, i) => (
            <li key={`${f.name}-${i}`} className="flex items-center gap-1.5 text-xs rounded-full px-2.5 py-1"
              style={{ backgroundColor: 'var(--color-hover-row)' }}>
              <Paperclip size={12} /><span className="max-w-[160px] truncate">{f.name}</span>
              <button type="button" aria-label={`Remove ${f.name}`} onClick={() => setFiles(files.filter((_, j) => j !== i))}>
                <X size={12} />
              </button>
            </li>
          ))}
        </ul>
      )}
      {(errors.body?.message || fileError) && (
        <p className="text-xs text-red-500" role="alert">{errors.body?.message ?? fileError}</p>
      )}
      <div className="flex items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <input ref={picker} type="file" multiple accept={ACCEPT} className="hidden"
            onChange={e => addFiles(e.target.files)} />
          <button type="button" className="btn-ghost px-2.5 py-1.5" onClick={() => picker.current?.click()}
            disabled={isSending || files.length >= MAX_FILES}>
            <Paperclip size={15} /> Attach
          </button>
          <span className={`text-xs ${length > MAX_BODY ? 'text-red-500' : 'text-gray-400'}`}>{length}/{MAX_BODY}</span>
        </div>
        <button type="submit" className="btn-primary" disabled={isSending}>
          {isSending ? <Spinner className="w-4 h-4 !text-white" /> : <Send size={15} />} Send
        </button>
      </div>
    </form>
  )
}
