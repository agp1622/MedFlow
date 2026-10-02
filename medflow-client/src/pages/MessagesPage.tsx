import { useSearchParams } from 'react-router-dom'
import { ArrowLeft } from 'lucide-react'
import {
  useMessageThreads, useMessageThread, useSendMessage, useMarkMessagesRead, useUnreadMessages,
} from '@/hooks/queries'
import { messagesApi } from '@/api/services'
import { PageHeader } from '@/components/layout/AppLayout'
import { MessageConversation } from '@/components/messages/MessageConversation'
import { Avatar, EmptyState, PageSpinner } from '@/components/ui'
import { fmt } from '@/utils/format'

const asUtc = (d: string) => (/[zZ]|[+-]\d\d:?\d\d$/.test(d) ? d : `${d}Z`)

function Conversation({ patientId, name, onBack }: { patientId: number; name: string; onBack: () => void }) {
  const thread = useMessageThread(patientId)
  const send = useSendMessage(patientId)
  const markRead = useMarkMessagesRead()
  const unread = useUnreadMessages('Doctor')

  return (
    <div className="flex flex-col h-full min-h-0">
      <div className="flex items-center gap-3 pb-3 border-b border-border mb-2">
        <button className="md:hidden btn-ghost px-2 py-1.5" onClick={onBack} aria-label="Back to conversations">
          <ArrowLeft size={16} />
        </button>
        <Avatar name={name} size="sm" />
        <h2 className="font-bold text-gray-800 truncate">{name}</h2>
      </div>
      <MessageConversation
        messages={thread.data} isLoading={thread.isLoading} isError={thread.isError}
        otherName={name} unread={unread.data ?? 0}
        refetchThread={() => { thread.refetch() }}
        markRead={() => markRead.mutate(patientId)}
        send={(body, files) => send.mutateAsync({ body, files })}
        isSending={send.isPending}
        download={messagesApi.downloadAttachment}
      />
    </div>
  )
}

export function MessagesPage() {
  const threads = useMessageThreads()
  const [params, setParams] = useSearchParams()
  const selectedId = Number(params.get('patient')) || null
  const selected = threads.data?.find(t => t.patientId === selectedId)

  return (
    <div className="flex flex-col h-full overflow-hidden">
      <PageHeader title="Messages" subtitle="Conversations with your patients" />
      {threads.isLoading ? <PageSpinner /> : (
        <div className="flex-1 min-h-0 flex gap-5 px-4 md:px-8 py-6">
          <aside className={`card w-full md:w-80 flex-shrink-0 overflow-y-auto ${selected ? 'hidden md:block' : ''}`}>
            {threads.isError && <p className="text-sm text-red-500 p-5">Could not load conversations.</p>}
            {threads.data?.length === 0 && (
              <EmptyState title="No messages yet" description="Patient messages will appear here." />
            )}
            <ul className="divide-y divide-border">
              {threads.data?.map(t => (
                <li key={t.patientId}>
                  <button onClick={() => setParams({ patient: String(t.patientId) })}
                    className={`w-full text-left flex items-center gap-3 px-4 py-3 hover:bg-gray-50 ${
                      t.patientId === selectedId ? 'bg-gray-50' : ''}`}>
                    <Avatar name={t.patientName} size="sm" />
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center justify-between gap-2">
                        <p className={`text-sm truncate ${t.unreadCount ? 'font-bold' : 'font-semibold'} text-gray-800`}>
                          {t.patientName}
                        </p>
                        <span className="text-xs text-gray-400 flex-shrink-0">{fmt.dateShort(asUtc(t.lastMessageAt))}</span>
                      </div>
                      <p className="text-xs text-gray-400 truncate">{t.lastMessagePreview || 'Attachment'}</p>
                    </div>
                    {t.unreadCount > 0 && (
                      <span className="bg-primary-600 text-white text-xs font-bold rounded-full min-w-[20px] h-5 px-1.5 flex items-center justify-center"
                        aria-label={`${t.unreadCount} unread`}>{t.unreadCount}</span>
                    )}
                  </button>
                </li>
              ))}
            </ul>
          </aside>

          <section className={`card flex-1 min-w-0 p-5 ${selected ? '' : 'hidden md:flex md:items-center md:justify-center'}`}>
            {selected
              ? <Conversation key={selected.patientId} patientId={selected.patientId} name={selected.patientName}
                  onBack={() => setParams({})} />
              : <p className="text-sm text-gray-400">Select a conversation</p>}
          </section>
        </div>
      )}
    </div>
  )
}
