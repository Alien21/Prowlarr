import { some } from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { fetchIndexerSchema, selectIndexerSchema, setIndexerSchemaSort } from 'Store/Actions/indexerActions';
import createAllIndexersSelector from 'Store/Selectors/createAllIndexersSelector';
import createClientSideCollectionSelector from 'Store/Selectors/createClientSideCollectionSelector';
import AddIndexerModalContent from './AddIndexerModalContent';

function createMapStateToProps() {
  return createSelector(
    createClientSideCollectionSelector('indexers.schema'),
    createAllIndexersSelector(),
    (indexers, allIndexers) => {
      const {
        isFetching,
        isPopulated,
        error,
        items,
        sortDirection,
        sortKey
      } = indexers;

      const indexerList = items.map((item) => {
        const { definitionName } = item;
        return {
          ...item,
          isExistingIndexer: some(allIndexers, { definitionName })
        };
      });

      return {
        isFetching,
        isPopulated,
        error,
        indexers: indexerList,
        sortKey,
        sortDirection
      };
    }
  );
}

const mapDispatchToProps = {
  fetchIndexerSchema,
  selectIndexerSchema,
  setIndexerSchemaSort
};

class AddIndexerModalContentConnector extends Component {

  //
  // Lifecycle

  componentDidMount() {
    this.props.fetchIndexerSchema();
  }

  //
  // Listeners

  onIndexerSelect = ({ implementation, implementationName, name }) => {
    this.props.selectIndexerSchema({ implementation, implementationName, name });
    this.props.onSelectIndexer();
  };

  onSortPress = (sortKey, sortDirection) => {
    this.props.setIndexerSchemaSort({ sortKey, sortDirection });
  };

  //
  // Render

  render() {
    return (
      <AddIndexerModalContent
        {...this.props}
        onSortPress={this.onSortPress}
        onIndexerSelect={this.onIndexerSelect}
      />
    );
  }
}

AddIndexerModalContentConnector.propTypes = {
  fetchIndexerSchema: PropTypes.func.isRequired,
  selectIndexerSchema: PropTypes.func.isRequired,
  setIndexerSchemaSort: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired,
  onSelectIndexer: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(AddIndexerModalContentConnector);
