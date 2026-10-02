import { useEffect, useRef } from 'react'
import { MessageThreadView } from './MessageThreadView'
import { MessageComposer } from './MessageComposer'
import type { MessageDto } from '@/types'

interface Props {
  messages?: MessageDto[]
  isLoading: boolean
  isError: boolean
  otherName: string
  /** The viewer's current unread total (polled) */
  unread: number
  refetchThread: () => void
  markRead: () => void
  send: (body: string, files: File[]) => Promise<unknown>
  isSending: boolean
  download: (id: number, fileName: string) => Promise<void>
  showUrgentNotice?: boolean
}

/** Thread + composer, plus the read/refresh behaviour shared by the patient and doctor screens. */
export function MessageConversation(p: Props) {
  // Opening the thread (or receiving something new while it is open) marks the other party's messages read
  useEffect(() => {
    if (p.messages?.some(m => !m.isMine && !m.readAt)) p.markRead()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [p.messages])

  // A higher polled unread count means something arrived: reload the open thread
  const prevUnread = useRef<number | null>(null)
  useEffect(() => {
    if (prevUnread.current !== null && p.unread > prevUnread.current) p.refetchThread()
    prevUnread.current = p.unread
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [p.unread])

  return (
    <div className="flex flex-col min-h-0 h-full">
      <div className="flex-1 overflow-y-auto px-1">
        <MessageThreadView messages={p.messages} isLoading={p.isLoading} isError={p.isError}
          otherName={p.otherName} onDownload={p.download} />
      </div>
      <MessageComposer isSending={p.isSending} onSend={p.send} showUrgentNotice={p.showUrgentNotice} />
    </div>
  )
}
