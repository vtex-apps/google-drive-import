/* eslint-disable padding-line-between-statements */
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

const CHECK_URL = '/google-drive-import/owner-email'
const FETCH_URL = '/google-drive-import/import-images'
const REVOKE_URL = '/google-drive-import/revoke-token'

let initialCheck = false

const Admin: FC<WrappedComponentProps> = ({ intl }) => {
  const [state, setState] = useState<any>({
    fetching: false,
    revoking: false,
    fetched: false,
    authorization: null,
    loading: true,
  })

  const { fetching, revoking, fetched, authorization, loading } = state

  const fetch = () => {
    setState({
      ...state,
      fetching: true,
    })

    axios
      .get(FETCH_URL)
      .then((response: any) => {
        console.log('response', response)
        setState({
          ...state,
          fetching: false,
          fetched: response.data,
        })
        setTimeout(() => {
          setState({
            ...state,
            fetching: false,
            fetched: false,
          })
        }, 5000)
      })
      .catch(() => {
        setState({
          ...state,
          fetching: false,
          fetched: false,
        })
      })
  }

  const revoke = () => {
    setState({
      ...state,
      revoking: true,
    })

    axios
      .get(REVOKE_URL)
      .then(() => {
        setState({
          ...state,
          revoking: false,
          authorization: null,
        })
      })
      .catch(() => {
        setState({
          ...state,
          revoking: false,
        })
      })
  }

  useEffect(() => {
    if (!initialCheck) {
      initialCheck = true
      axios
        .get(CHECK_URL)
        .then((response: any) => {
          console.log('response', response)
          setState({
            ...state,
            loading: false,
            authorization: response.data,
          })
        })
        .catch(() => {
          setState({
            ...state,
            loading: false,
          })
        })
    }
  })

  console.log('authorization', authorization)

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
              <p>
                <FormattedMessage id="admin/google-drive-import.connected.text" />{' '}
                <div className="mt4">
                  <Button
                    variation="primary"
                    collapseLeft
                    isLoading={fetching}
                    onClick={() => {
                      fetch()
                    }}
                  >
                    <FormattedMessage id="admin/google-drive-import.fetch.button" />
                  </Button>
                </div>
              </p>

              {!fetching && fetched && <p>{`${fetched}`}</p>}
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
                <strong>{`${authorization}`}</strong>
                <div className="mt4">
                  <Button
                    variation="danger-tertiary"
                    size="regular"
                    isLoading={revoking}
                    onClick={() => {
                      revoke()
                    }}
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
