/* eslint-disable no-console */
/* eslint-disable @typescript-eslint/no-explicit-any */
import React, { FC, useState } from 'react'
import { useRuntime } from 'vtex.render-runtime'
import {
  Layout,
  PageHeader,
  Card,
  Button,
  ButtonPlain,
  Spinner,
  Divider,
  Tabs,
  Tab,
} from 'vtex.styleguide'
import { injectIntl, FormattedMessage, WrappedComponentProps } from 'react-intl'
import { compose, graphql, useQuery, useMutation } from 'react-apollo'

import styles from './styles.css'
import GoogleSignIn from '../public/metadata/google_signin.png'

import Q_OWNER_EMAIL from './queries/GetOwnerEmail.gql'
import Q_HAVE_TOKEN from './queries/HaveToken.gql'
import Q_SHEET_LINK from './queries/SheetLink.gql'

import M_REVOKE from './mutations/RevokeToken.gql'
// import M_AUTHORIZE from './mutations/GoogleAuthorize.gql'
import M_CREATE_SHEET from './mutations/CreateSheet.gql'
import M_PROCESS_SHEET from './mutations/ProcessSheet.gql'
import M_ADD_IMAGES from './mutations/AddImages.gql'
import M_IMPORT_IMAGES from './mutations/ImportImages.gql'

const AUTH_URL = '/google-drive-import/auth'

const Admin: FC<WrappedComponentProps & any> = ({
  intl,
  link,
  token,
  owner,
}) => {
  console.log('Link =>', link)
  console.log('Token =>', token)
  console.log('Owner =>', owner)
  const [state, setState] = useState<any>({
    currentTab: 1,
  })

  const { account } = useRuntime()

  const {
    loading: ownerLoading,
    called: ownerCalled,
    data: ownerData,
  } = useQuery(Q_OWNER_EMAIL, {
    variables: {
      accountName: account,
    },
  })

  const [revoke, { loading: revokeLoading }] = useMutation(M_REVOKE, {
    onCompleted: (ret: any) => {
      console.log('Revoke =>', ret)
      if (ret.revokeToken === true) {
        window.location.reload()
      }
    },
  })

  const [
    create,
    { loading: createLoading, data: createData, called: createCalled },
  ] = useMutation(M_CREATE_SHEET)

  const [fetch, { loading: fetching, data: fetched }] = useMutation(
    M_IMPORT_IMAGES
  )

  const [
    sheetImport,
    { loading: sheetProcessing, data: sheetProcessed },
  ] = useMutation(M_PROCESS_SHEET)

  const [addImages, { loading: addingImages, data: imagesAdded }] = useMutation(
    M_ADD_IMAGES
  )

  const { currentTab } = state

  const auth = () => {
    revoke()
      .then(() => {
        window.top.location.href = AUTH_URL
      })
      .catch(() => {
        window.top.location.href = AUTH_URL
      })
  }

  const changeTab = (tab: number) => {
    setState({
      ...state,
      currentTab: tab,
    })
  }

  const showLink = () => {
    return (
      (link.called && !link.loading && link.sheetLink) ||
      (createCalled && !createLoading && !!createData?.createSheet)
    )
  }

  return (
    <Layout
      pageHeader={
        <div className="flex justify-center">
          <div className="w-100 mw-reviews-header">
            <PageHeader
              title={intl.formatMessage({
                id: 'admin/google-drive-import.title',
              })}
            >
              {token.called && !token.loading && token.haveToken === true && (
                <div>
                  {ownerCalled && !ownerLoading && ownerData && (
                    <p>
                      <FormattedMessage id="admin/google-drive-import.connected-as" />{' '}
                      <strong>{`${ownerData.getOwnerEmail}`}</strong>
                    </p>
                  )}
                  <div className="mt4 mb4 tr">
                    <Button
                      variation="danger-tertiary"
                      size="regular"
                      isLoading={revokeLoading}
                      onClick={() => {
                        revoke({
                          variables: {
                            accountName: account,
                          },
                        })
                      }}
                      collapseLeft
                    >
                      <FormattedMessage id="admin/google-drive-import.disconnect.button" />
                    </Button>
                  </div>
                </div>
              )}
            </PageHeader>
          </div>
        </div>
      }
      fullWidth
    >
      {token.called && (
        <div>
          {token.loading && (
            <div className="pv6">
              <Spinner />
            </div>
          )}
          {!token.loading && token.haveToken !== true && (
            <div>
              <Card>
                <h2>
                  <FormattedMessage id="admin/google-drive-import.setup.title" />
                </h2>
                <p>
                  <FormattedMessage id="admin/google-drive-import.setup.description" />{' '}
                  <div className="mt4">
                    <ButtonPlain
                      variation="primary"
                      collapseLeft
                      onClick={() => {
                        auth()
                      }}
                    >
                      <img src={GoogleSignIn} alt="Sign in with Google" />
                    </ButtonPlain>
                  </div>
                </p>
              </Card>
            </div>
          )}
        </div>
      )}
      {token.called && !token.loading && token.haveToken === true && (
        <Tabs fullWidth>
          <Tab
            label="Instructions"
            active={currentTab === 1}
            onClick={() => changeTab(1)}
          >
            <div>
              <Card>
                <div className="flex">
                  <div className="w-100">
                    <p>
                      <FormattedMessage
                        id="admin/google-drive-import.connected.text"
                        values={{ lineBreak: <br /> }}
                      />
                    </p>
                    <pre className={`${styles.code}`}>
                      <FormattedMessage
                        id="admin/google-drive-import.folder-structure"
                        values={{ lineBreak: <br />, account }}
                      />
                    </pre>
                    <p>
                      There are two ways to associate images to SKUs:{' '}
                      <strong>
                        <a href="#skuimages" className="link black-90">
                          Standardized Naming
                        </a>
                      </strong>{' '}
                      (SKU Images) and{' '}
                      <strong>
                        <a href="#spreadsheet" className="link black-90">
                          Spreadsheet
                        </a>
                      </strong>
                    </p>
                    <Divider />
                    <h2 className="heading-3 mt4 mb4" id="skuimages">
                      <FormattedMessage id="admin/google-drive-import.sku-images.title" />
                    </h2>
                    <p>
                      <FormattedMessage id="admin/google-drive-import.instructions-line-01" />
                    </p>

                    <table
                      className={`${styles.borderCollapse} ba collapse w-100`}
                    >
                      <thead>
                        <tr>
                          <th />
                          <th className="pa4">Description</th>
                        </tr>
                      </thead>
                      <tbody>
                        <tr>
                          <th className="flex justify-left bt items-center pa4">
                            IdType
                          </th>
                          <td className="bt bl pa4">
                            <FormattedMessage id="admin/google-drive-import.instructions-description-IdType" />
                          </td>
                        </tr>
                        <tr className={`${styles.striped}`}>
                          <th className="flex justify-left bt pa4">Id</th>
                          <td className="bt bl pa4">
                            <FormattedMessage id="admin/google-drive-import.instructions-description-Id" />
                          </td>
                        </tr>
                        <tr>
                          <th className="flex justify-left bt items-center pa4">
                            ImageName
                          </th>
                          <td className="bt bl pa4">
                            <FormattedMessage id="admin/google-drive-import.instructions-description-ImageName" />
                          </td>
                        </tr>
                        <tr className={`${styles.striped}`}>
                          <th className="flex justify-left bt pa4">
                            ImageLabel
                          </th>
                          <td className="bt bl pa4">
                            <FormattedMessage id="admin/google-drive-import.instructions-description-ImageLabel" />
                          </td>
                        </tr>
                        <tr className={`${styles.striped}`}>
                          <th className="flex justify-left bt pa4">Main?</th>
                          <td className="bt bl pa4">
                            <FormattedMessage id="admin/google-drive-import.instructions-description-Main" />
                          </td>
                        </tr>
                        <tr className={`${styles.striped}`}>
                          <th className="flex justify-left bt pa4">
                            Specification
                          </th>
                          <td className="bt bl pa4">
                            <FormattedMessage id="admin/google-drive-import.instructions-description-Spec" />
                          </td>
                        </tr>
                      </tbody>
                    </table>
                    <p>
                      <strong>
                        <FormattedMessage
                          id="admin/google-drive-import.instructions-examples"
                          values={{ lineBreak: <br /> }}
                        />
                      </strong>
                    </p>
                    <p>
                      <FormattedMessage id="admin/google-drive-import.instructions-line-02" />
                    </p>
                    <p>
                      <FormattedMessage id="admin/google-drive-import.instructions-line-03" />
                    </p>

                    <Divider />
                    <h2 id="spreadsheet">Spreadsheet</h2>
                    <p>
                      Instead of renaming all the images, you can use a
                      Spreadsheet to make the bind between SKUs and Images. On
                      the Actions tab you'll find the button to create a
                      Spreadsheet on your account, after that a link will be
                      shown to led you directly to the file. Detailed
                      instructions can be found on the Spreadsheet
                      "Instructions" tab
                    </p>
                  </div>
                </div>
              </Card>
            </div>
          </Tab>
          <Tab
            label="Actions"
            active={currentTab === 2}
            onClick={() => changeTab(2)}
          >
            <div className="bg-base pa8">
              <h2>SKU Images</h2>
              <Card>
                <div className="flex">
                  <div className="w-70">
                    <p>
                      The App fetches new images automatically, but you can
                      force it to fetch new images immediately
                    </p>
                  </div>

                  <div
                    style={{ flexGrow: 1 }}
                    className="flex items-stretch w-20 justify-center"
                  >
                    <Divider orientation="vertical" />
                  </div>

                  <div className="w-30 items-center flex">
                    {!fetched?.importImages && (
                      <Button
                        variation="primary"
                        collapseLeft
                        block
                        isLoading={fetching}
                        onClick={() => {
                          fetch()
                        }}
                      >
                        <FormattedMessage id="admin/google-drive-import.fetch.button" />
                      </Button>
                    )}

                    {!fetching && fetched?.importImages && (
                      <p className="block">
                        <strong>{`${fetched.importImages}`}</strong>
                      </p>
                    )}
                  </div>
                </div>
              </Card>
              <br />
              <h2>Spreadsheet</h2>
              {!createData && link.called && !link.loading && !link.sheetLink && (
                <Card>
                  <div className="flex">
                    <div className="w-70">
                      <p>
                        Creates a Sheet with a default structure that you need
                        for the mapping
                      </p>
                    </div>

                    <div
                      style={{ flexGrow: 1 }}
                      className="flex items-stretch w-20 justify-center"
                    >
                      <Divider orientation="vertical" />
                    </div>

                    <div className="w-30 items-center flex">
                      <Button
                        variation="primary"
                        collapseLeft
                        block
                        isLoading={createLoading}
                        onClick={() => {
                          create()
                        }}
                      >
                        <FormattedMessage id="admin/google-drive-import.create-sheet.button" />
                      </Button>
                    </div>
                  </div>
                </Card>
              )}
              {showLink() && (
                <Card>
                  <div className="flex">
                    <div className="w-100">
                      <p>
                        Access the mapping Spreadsheet{' '}
                        <a
                          href={createData?.createSheet || link.sheetLink}
                          target="_blank"
                          rel="noreferrer"
                        >
                          {createData?.createSheet || link.sheetLink}
                        </a>
                      </p>
                    </div>
                  </div>
                </Card>
              )}
              <br />
              {showLink() && (
                <div>
                  <Card>
                    <div className="flex">
                      <div className="w-70">
                        <p>
                          Starts the image importing process based on the
                          mapping defined at the Spreadsheet
                        </p>
                      </div>
                      <div
                        style={{ flexGrow: 1 }}
                        className="flex items-stretch w-20 justify-center"
                      >
                        <Divider orientation="vertical" />
                      </div>
                      <div className="w-30 items-center flex">
                        {!sheetProcessed?.processSheet && (
                          <Button
                            variation="primary"
                            collapseLeft
                            block
                            isLoading={sheetProcessing}
                            onClick={() => {
                              sheetImport()
                            }}
                          >
                            <FormattedMessage id="admin/google-drive-import.sheet-import.button" />
                          </Button>
                        )}
                        {!sheetProcessing && sheetProcessed?.processSheet && (
                          <p>
                            <strong>{`${sheetProcessed.processSheet}`}</strong>
                          </p>
                        )}
                      </div>
                    </div>
                  </Card>
                  <br />
                  <Card>
                    <div className="flex">
                      <div className="w-70">
                        <p>
                          Clears the Spreadsheet and fills the image names and
                          thumbnails automatically based on the files at the{' '}
                          <strong>NEW</strong> folder
                        </p>
                      </div>
                      <div
                        style={{ flexGrow: 1 }}
                        className="flex items-stretch w-20 justify-center"
                      >
                        <Divider orientation="vertical" />
                      </div>
                      <div className="w-30 items-center flex">
                        {!imagesAdded?.addImages && (
                          <Button
                            variation="primary"
                            collapseLeft
                            block
                            isLoading={addingImages}
                            onClick={() => {
                              addImages()
                            }}
                          >
                            <FormattedMessage id="admin/google-drive-import.add-images.button" />
                          </Button>
                        )}
                        {!addingImages && imagesAdded?.addImages && (
                          <p>
                            <strong>Process initiated</strong>
                          </p>
                        )}
                      </div>
                    </div>
                  </Card>
                </div>
              )}
            </div>
          </Tab>
        </Tabs>
      )}
    </Layout>
  )
}

const token = {
  name: 'token',
  options: () => ({
    ssr: false,
  }),
}

const link = {
  name: 'link',
  options: () => ({
    ssr: false,
  }),
}

export default injectIntl(
  compose(graphql(Q_HAVE_TOKEN, token), graphql(Q_SHEET_LINK, link))(Admin)
)
