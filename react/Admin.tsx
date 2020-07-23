/* eslint-disable no-console */
import React, { FC, useState, useEffect } from 'react'
import axios from 'axios'

import {
  Layout,
  PageHeader,
  Card,
  Button,
  Divider,
  Spinner,
} from 'vtex.styleguide'
import { injectIntl, FormattedMessage, WrappedComponentProps } from 'react-intl'

const CHECK_URL = '/google-drive-import/have-token'
let initialCheck = false

const Admin: FC<WrappedComponentProps> = ({ intl }) => {
  const [state, setState] = useState<any>({
    fetching: false,
    fetched: false,
    authorization: null,
    loading: true,
  })

  const { fetching, fetched, authorization, loading } = state

  const fetch = () => {
    setState({
      ...state,
      fetching: true,
    })

    setTimeout(() => {
      setState({
        ...state,
        fetching: false,
        fetched: true,
      })
    }, 3000)

    setTimeout(() => {
      setState({
        ...state,
        fetching: false,
        fetched: false,
      })
    }, 10000)
  }

  useEffect(() => {
    if (!initialCheck) {
      initialCheck = true
      axios.get(CHECK_URL).then((response: any) => {
        setState({ ...state, loading: false, authorization: response.data })
      })
    }
  })

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
        {authorization && (
          <div className="flex">
            <div className="w-40">
              {fetching && (
                <div className="pv6">
                  <Spinner />
                </div>
              )}
              {!fetching && !fetched && (
                <p>
                  <FormattedMessage id="admin/google-drive-import.connected.text" />{' '}
                  <div className="mt4">
                    <Button
                      variation="primary"
                      collapseLeft
                      onClick={() => {
                        fetch()
                      }}
                    >
                      <FormattedMessage id="admin/google-drive-import.fetch.button" />
                    </Button>
                  </div>
                </p>
              )}
              {!fetching && fetched && (
                <p>
                  <FormattedMessage id="admin/google-drive-import.fetched.text" />{' '}
                </p>
              )}
            </div>
            <div
              style={{ flexGrow: 1 }}
              className="flex items-stretch w-20 justify-center"
            >
              <Divider orientation="vertical" />
            </div>
            <div className="w-40">
              <p>
                <FormattedMessage id="admin/google-drive-import.connected-as" />{' '}
                {' {email} '}
                <div className="mt4">
                  <Button
                    variation="danger-tertiary"
                    size="regular"
                    collapseLeft
                  >
                    <FormattedMessage id="admin/google-drive-import.disconnect.button" />
                  </Button>
                </div>
              </p>
            </div>
          </div>
        )}

        {!authorization && (
          <div>
            {loading && (
              <div className="pv6">
                <Spinner />
              </div>
            )}
            {!loading && (
              <div>
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
              </div>
            )}
          </div>
        )}
      </Card>
    </Layout>
  )
}

export default injectIntl(Admin)
