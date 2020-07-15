import React, { FC } from 'react'
import { Layout, PageHeader, Card, Button } from 'vtex.styleguide'
import { injectIntl, FormattedMessage, WrappedComponentProps } from 'react-intl'

const Admin: FC<WrappedComponentProps> = ({ intl }) => {
  return (
    <Layout
      pageHeader={
        <div className="flex justify-center">
          <div className="w-100 mw-reviews-header">
            <PageHeader
              title={intl.formatMessage({
                id: 'admin/google-drive-import.title',
              })}
            />
          </div>
        </div>
      }
      fullWidth
    >
      <Card>
        <h2>
          <FormattedMessage id="admin/google-drive-import.setup.title" />
        </h2>
        <p>
          <FormattedMessage id="admin/google-drive-import.setup.description" />{' '}
          <div className="mt4">
            <Button
              variation="primary"
              collapseLeft
              href="/google-drive-import/auth"
              target="_top"
            >
              <FormattedMessage id="admin/google-drive-import.setup.button" />
            </Button>
          </div>
        </p>
      </Card>
    </Layout>
  )
}

export default injectIntl(Admin)
