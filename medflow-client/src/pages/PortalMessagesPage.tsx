import {
  usePortalMe, usePortalMessages, useSendPortalMessage, useMarkPortalMessagesRead, useUnreadMessages,
} from '@/hooks/queries'
import { portalApi } from '@/api/services'
import { MessageConversation } from '@/components/messages/MessageConversation'

export function PortalMessagesPage() {
  const me = usePortalMe()
  const thread = usePortalMessages()
  const send = useSendPortalMessage()
  const markRead = useMarkPortalMessagesRead()
  const unread = useUnreadMessages('Patient')
  const doctor = me.data?.doctorName ?? 'Your doctor'

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-bold text-gray-900">Messages</h1>
        <p className="text-sm text-gray-500 mt-1">Your conversation with {doctor}</p>
      </div>
      <section className="card p-5 flex flex-col h-[calc(100vh-14rem)] min-h-[24rem]">
        <MessageConversation
          messages={thread.data} isLoading={thread.isLoading} isError={thread.isError}
          otherName={doctor} unread={unread.data ?? 0}
          refetchThread={() => { thread.refetch() }}
          markRead={() => markRead.mutate()}
          send={(body, files) => send.mutateAsync({ body, files })}
          isSending={send.isPending}
          download={portalApi.downloadMessageAttachment}
          showUrgentNotice
        />
      </section>
    </div>
  )
}
