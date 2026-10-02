import { useEffect, useRef, useState } from 'react'
import { Download, Paperclip } from 'lucide-react'
import toast from 'react-hot-toast'
import { Spinner } from '@/components/ui'
import { fmt } from '@/utils/format'
import type { MessageDto } from '@/types'

// The API sends UTC times without a zone suffix; treat them as UTC so they display in local time
const asUtc = (d: string) => (/[zZ]|[+-]\d\d:?\d\d$/.test(d) ? d : `${d}Z`)

function formatSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

interface Props {
  messages?: MessageDto[]
  isLoading: boolean
  isError: boolean
  /** Name shown above the other party's messages */
  otherName: string
  onDownload: (id: number, fileName: string) => Promise<void>
}

export function MessageThreadView({ messages, isLoading, isError, otherName, onDownload }: Props) {
  const endRef = useRef<HTMLDivElement>(null)
  const [downloading, setDownloading] = useState<number | null>(null)

  useEffect(() => { endRef.current?.scrollIntoView({ block: 'end' }) }, [messages?.length])

  const download = async (id: number, fileName: string) => {
    setDownloading(id)
    try { await onDownload(id, fileName) }
    catch { toast.error('Could not download this file') }
    finally { setDownloading(null) }
  }

  if (isLoading) return <div className="flex justify-center py-10"><Spinner className="w-6 h-6" /></div>
  if (isError) return <p className="text-sm text-red-500 py-6 text-center">Could not load messages. Please try again later.</p>
  if (!messages || messages.length === 0)
    return <p className="text-sm text-gray-400 py-10 text-center">No messages yet. Write the first one below.</p>

  return (
    <ol className="space-y-3 py-2" aria-label="Messages">
      {messages.map(m => (
        <li key={m.id} className={`flex flex-col ${m.isMine ? 'items-end' : 'items-start'}`}>
          <span className="text-xs text-gray-400 mb-1">
            {m.isMine ? 'You' : otherName} · {fmt.dateTime(asUtc(m.sentAt))}
          </span>
          <div
            className={`max-w-[85%] sm:max-w-[70%] rounded-2xl px-4 py-2.5 text-sm space-y-2 ${m.isMine ? 'bg-primary-600 text-white' : ''}`}
            style={m.isMine ? undefined : { backgroundColor: 'var(--color-hover-row)', color: 'var(--color-text-primary)' }}
          >
            {m.body && <p className="whitespace-pre-wrap break-words">{m.body}</p>}
            {m.attachments.map(a => (
              <button key={a.id} type="button" onClick={() => download(a.id, a.fileName)}
                disabled={downloading === a.id}
                className={`flex items-center gap-2 w-full text-left rounded-lg px-2.5 py-1.5 text-xs font-medium disabled:opacity-60 ${
                  m.isMine ? 'bg-white/15 hover:bg-white/25' : 'bg-black/5 hover:bg-black/10'}`}>
                <Paperclip size={13} className="flex-shrink-0" />
                <span className="truncate flex-1">{a.fileName}</span>
                <span className="opacity-70 flex-shrink-0">{formatSize(a.fileSize)}</span>
                {downloading === a.id ? <Spinner className="w-3.5 h-3.5" /> : <Download size={13} className="flex-shrink-0" />}
              </button>
            ))}
          </div>
        </li>
      ))}
      <div ref={endRef} />
    </ol>
  )
}
